using StargateAPI.Business.Data;
using StargateAPI.Business.Results;

namespace StargateAPI.Business.Domain
{
    /// <summary>
    /// A person's current astronaut state as loaded by the caller: the summary, the open duty, and
    /// the latest duty start in the whole history. Passed explicitly so "no assignments" is never
    /// inferred from an unloaded navigation property.
    /// </summary>
    public sealed record CareerState(AstronautDetail? Detail, AstronautDuty? OpenDuty, DateOnly? LatestDutyStart);

    /// <summary>The changes made by an assignment: entities the caller must add, and the duty it closed.</summary>
    public sealed record DutyAssignment(AstronautDuty NewDuty, AstronautDetail? NewDetail, AstronautDuty? ClosedDuty);

    /// <summary>
    /// Career transition rules. Validates the whole transition before changing anything, so a
    /// rejected assignment leaves the loaded entities untouched. No I/O: persistence, transactions
    /// and concurrency belong to the command handler, and database constraints back these rules up.
    /// </summary>
    public static class CareerTransitions
    {
        public static Result<DutyAssignment> AssignDuty(Person person, CareerState state, DutyAssignmentInput input)
        {
            EnsureConsistent(person, state);

            if (state.OpenDuty is null)
            {
                return StartCareer(person, input);
            }

            var openDuty = state.OpenDuty;
            var detail = state.Detail!;

            // Rule 5 needs strictly increasing starts: the previous duty ends the day before.
            if (input.StartDate <= state.LatestDutyStart)
            {
                return AppError.Conflict(ErrorCodes.ChronologyConflict,
                    $"A new duty must start after the current duty's start date ({state.LatestDutyStart:yyyy-MM-dd}).");
            }

            // A second retirement would move the career end through a period with no active service.
            if (input.IsRetirement && detail.CurrentDutyTitle == DutyAssignmentInput.RetiredTitle)
            {
                return AppError.Conflict(ErrorCodes.AlreadyRetired,
                    "This person is already retired. Assign an active duty before retiring again.");
            }

            openDuty.Close(input.StartDate.AddDays(-1));

            // Rule 7: career end is the day before retirement starts. An active duty, including a
            // reactivation after retirement, clears it. CareerStartDate (first service) is unchanged.
            DateOnly? careerEnd = input.IsRetirement ? input.StartDate.AddDays(-1) : null;
            detail.ApplyCurrentDuty(input.Rank, input.DutyTitle, input.StartDate, careerEnd);

            var newDuty = AstronautDuty.Start(person.Id, input.Rank, input.DutyTitle, input.StartDate);
            person.Touch();
            return new DutyAssignment(newDuty, NewDetail: null, ClosedDuty: openDuty);
        }

        private static Result<DutyAssignment> StartCareer(Person person, DutyAssignmentInput input)
        {
            // Retiring as a first assignment would put the career end before the career start.
            if (input.IsRetirement)
            {
                return AppError.Conflict(ErrorCodes.FirstDutyRetirement,
                    "A person's first duty cannot be a retirement.");
            }

            var detail = AstronautDetail.StartCareer(person.Id, input.Rank, input.DutyTitle, input.StartDate);
            var duty = AstronautDuty.Start(person.Id, input.Rank, input.DutyTitle, input.StartDate);
            person.Touch();
            return new DutyAssignment(duty, detail, ClosedDuty: null);
        }

        /// <summary>
        /// Rules 2-4 as an invariant: no detail and no duties, or one detail plus exactly one open
        /// duty that is also the latest. Anything else is corrupt data, not a client error.
        /// </summary>
        private static void EnsureConsistent(Person person, CareerState state)
        {
            var consistent = (state.Detail, state.OpenDuty, state.LatestDutyStart) switch
            {
                (null, null, null) => true,
                ({ } detail, { } open, { } latest) =>
                    detail.PersonId == person.Id && open.PersonId == person.Id && open.DutyStartDate == latest,
                _ => false
            };

            if (!consistent)
            {
                throw new InvalidOperationException(
                    $"Inconsistent astronaut state for person {person.Id}: detail, open duty and history disagree.");
            }
        }
    }
}
