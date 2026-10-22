export type PossibleTypesResultData = {
  possibleTypes: {
    AssignDutyError: [
      'AlreadyRetiredError',
      'AmbiguousNameError',
      'ChronologyConflictError',
      'ConcurrencyConflictError',
      'FirstDutyRetirementError',
      'NotFoundError',
      'ValidationError',
    ];
    CreatePersonError: ['ValidationError'];
    Error: [
      'AlreadyRetiredError',
      'AmbiguousNameError',
      'ChronologyConflictError',
      'ConcurrencyConflictError',
      'FirstDutyRetirementError',
      'NotFoundError',
      'ValidationError',
      'VersionRequiredError',
    ];
    PersonByNameResult: ['AmbiguousNameError', 'NotFoundError', 'Person', 'ValidationError'];
    UpdatePersonError: ['ConcurrencyConflictError', 'NotFoundError', 'ValidationError'];
    UpsertPersonByNameError: [
      'AmbiguousNameError',
      'ConcurrencyConflictError',
      'NotFoundError',
      'ValidationError',
      'VersionRequiredError',
    ];
  };
};
const result: PossibleTypesResultData = {
  possibleTypes: {
    AssignDutyError: [
      'AlreadyRetiredError',
      'AmbiguousNameError',
      'ChronologyConflictError',
      'ConcurrencyConflictError',
      'FirstDutyRetirementError',
      'NotFoundError',
      'ValidationError',
    ],
    CreatePersonError: ['ValidationError'],
    Error: [
      'AlreadyRetiredError',
      'AmbiguousNameError',
      'ChronologyConflictError',
      'ConcurrencyConflictError',
      'FirstDutyRetirementError',
      'NotFoundError',
      'ValidationError',
      'VersionRequiredError',
    ],
    PersonByNameResult: ['AmbiguousNameError', 'NotFoundError', 'Person', 'ValidationError'],
    UpdatePersonError: ['ConcurrencyConflictError', 'NotFoundError', 'ValidationError'],
    UpsertPersonByNameError: [
      'AmbiguousNameError',
      'ConcurrencyConflictError',
      'NotFoundError',
      'ValidationError',
      'VersionRequiredError',
    ],
  },
};
export default result;
