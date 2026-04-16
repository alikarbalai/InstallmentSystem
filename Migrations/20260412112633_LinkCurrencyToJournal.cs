using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstallmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class LinkCurrencyToJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "JournalEntries",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CurrencyId",
                table: "JournalEntries",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Currencies_CurrencyId",
                table: "JournalEntries",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Currencies_CurrencyId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_CurrencyId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "JournalEntries");
        }
    }
}
