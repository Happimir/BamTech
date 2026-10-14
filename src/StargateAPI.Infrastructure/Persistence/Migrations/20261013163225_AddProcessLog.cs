using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StargateAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    ActorSubject = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ActorIssuer = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SourceSystem = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AstronautDutyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExceptionType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ExceptionDetails = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: true),
                    ChangesJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessLog", x => x.Id);
                    table.CheckConstraint("CK_ProcessLog_Audit_Succeeded", "\"Phase\" <> 'MutationAudit' OR \"Outcome\" = 'Succeeded'");
                    table.CheckConstraint("CK_ProcessLog_Duration", "\"DurationMilliseconds\" IS NULL OR \"DurationMilliseconds\" >= 0");
                    table.CheckConstraint("CK_ProcessLog_HttpStatus", "\"HttpStatusCode\" IS NULL OR \"HttpStatusCode\" BETWEEN 100 AND 599");
                    table.CheckConstraint("CK_ProcessLog_Lengths", "length(\"TraceId\") <= 64 AND length(\"Operation\") <= 128 AND length(\"Message\") <= 2048 AND (\"ErrorCode\" IS NULL OR length(\"ErrorCode\") <= 128) AND (\"ExceptionType\" IS NULL OR length(\"ExceptionType\") <= 512) AND (\"ExceptionDetails\" IS NULL OR length(\"ExceptionDetails\") <= 16000) AND (\"ChangesJson\" IS NULL OR length(\"ChangesJson\") <= 8000)");
                    table.CheckConstraint("CK_ProcessLog_Outcome", "\"Outcome\" IN ('Succeeded', 'Rejected', 'Failed', 'Cancelled')");
                    table.CheckConstraint("CK_ProcessLog_Phase", "\"Phase\" IN ('MutationAudit', 'RequestOutcome')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLog_OccurredAtUtc_Id",
                table: "ProcessLog",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLog_OperationId_Phase",
                table: "ProcessLog",
                columns: new[] { "OperationId", "Phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLog_PersonId_OccurredAtUtc",
                table: "ProcessLog",
                columns: new[] { "PersonId", "OccurredAtUtc" },
                filter: "\"PersonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLog_TraceId_OccurredAtUtc",
                table: "ProcessLog",
                columns: new[] { "TraceId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessLog");
        }
    }
}
