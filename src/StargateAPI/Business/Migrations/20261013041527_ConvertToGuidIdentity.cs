using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StargateAPI.Business.Migrations
{
    /// <summary>
    /// Converts integer keys to application-generated UUIDv7 Guids, calendar dates to DateOnly,
    /// and adds name matching, structured names, the concurrency version, the current duty start
    /// date and history constraints.
    ///
    /// Supported for EMPTY databases only (fresh setup). Converting populated data requires one
    /// persisted old-to-new ID mapping per entity, history validation and a summary backfill,
    /// which SQL alone cannot do; that conversion is not implemented or verified. Rather than let
    /// the table rebuilds copy integer IDs into Guid columns, the migration aborts before making
    /// any change when legacy rows exist.
    /// </summary>
    public partial class ConvertToGuidIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AbortIfAnyRows(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_AstronautDuty_PersonId",
                table: "AstronautDuty");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Person",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Person",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Person",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Person",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Person",
                type: "TEXT",
                maxLength: 1200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Person",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "PersonId",
                table: "AstronautDuty",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "AstronautDuty",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PersonId",
                table: "AstronautDetail",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "CareerStartDate",
                table: "AstronautDetail",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "AstronautDetail",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CurrentDutyStartDate",
                table: "AstronautDetail",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_Person_NormalizedName_Id",
                table: "Person",
                columns: new[] { "NormalizedName", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Person_Name_NotBlank",
                table: "Person",
                sql: "length(trim(\"Name\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Person_NormalizedName_NotBlank",
                table: "Person",
                sql: "length(trim(\"NormalizedName\")) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_AstronautDuty_PersonId_DutyStartDate",
                table: "AstronautDuty",
                columns: new[] { "PersonId", "DutyStartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AstronautDuty_PersonId_Open",
                table: "AstronautDuty",
                column: "PersonId",
                unique: true,
                filter: "\"DutyEndDate\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AstronautDuty_DutyTitle_NotBlank",
                table: "AstronautDuty",
                sql: "length(trim(\"DutyTitle\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AstronautDuty_End_AfterStart",
                table: "AstronautDuty",
                sql: "\"DutyEndDate\" IS NULL OR \"DutyEndDate\" >= \"DutyStartDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AstronautDuty_Rank_NotBlank",
                table: "AstronautDuty",
                sql: "length(trim(\"Rank\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AstronautDetail_CareerEnd_AfterStart",
                table: "AstronautDetail",
                sql: "\"CareerEndDate\" IS NULL OR \"CareerEndDate\" >= \"CareerStartDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AstronautDetail_CurrentDutyTitle_NotBlank",
                table: "AstronautDetail",
                sql: "length(trim(\"CurrentDutyTitle\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AstronautDetail_CurrentRank_NotBlank",
                table: "AstronautDetail",
                sql: "length(trim(\"CurrentRank\")) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Guid keys cannot be converted back to integers without loss.
            AbortIfAnyRows(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_Person_NormalizedName_Id",
                table: "Person");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Person_Name_NotBlank",
                table: "Person");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Person_NormalizedName_NotBlank",
                table: "Person");

            migrationBuilder.DropIndex(
                name: "IX_AstronautDuty_PersonId_DutyStartDate",
                table: "AstronautDuty");

            migrationBuilder.DropIndex(
                name: "IX_AstronautDuty_PersonId_Open",
                table: "AstronautDuty");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AstronautDuty_DutyTitle_NotBlank",
                table: "AstronautDuty");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AstronautDuty_End_AfterStart",
                table: "AstronautDuty");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AstronautDuty_Rank_NotBlank",
                table: "AstronautDuty");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AstronautDetail_CareerEnd_AfterStart",
                table: "AstronautDetail");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AstronautDetail_CurrentDutyTitle_NotBlank",
                table: "AstronautDetail");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AstronautDetail_CurrentRank_NotBlank",
                table: "AstronautDetail");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Person");

            migrationBuilder.DropColumn(
                name: "CurrentDutyStartDate",
                table: "AstronautDetail");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Person",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "PersonId",
                table: "AstronautDuty",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "AstronautDuty",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "PersonId",
                table: "AstronautDetail",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CareerStartDate",
                table: "AstronautDetail",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "AstronautDetail",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.CreateIndex(
                name: "IX_AstronautDuty_PersonId",
                table: "AstronautDuty",
                column: "PersonId");
        }

        // SQLite has no RAISE outside triggers, so a CHECK constraint on a temporary table
        // provides the abort. The constraint name appears in the resulting error message.
        private static void AbortIfAnyRows(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE TEMP TABLE \"__ConvertToGuidIdentityGuard\" (\"RowCount\" INTEGER NOT NULL " +
                "CONSTRAINT \"populated_database_conversion_not_supported\" CHECK (\"RowCount\" = 0));");
            migrationBuilder.Sql(
                "INSERT INTO \"__ConvertToGuidIdentityGuard\" (\"RowCount\") SELECT " +
                "(SELECT COUNT(*) FROM \"Person\") + (SELECT COUNT(*) FROM \"AstronautDetail\") + " +
                "(SELECT COUNT(*) FROM \"AstronautDuty\");");
            migrationBuilder.Sql("DROP TABLE \"__ConvertToGuidIdentityGuard\";");
        }
    }
}
