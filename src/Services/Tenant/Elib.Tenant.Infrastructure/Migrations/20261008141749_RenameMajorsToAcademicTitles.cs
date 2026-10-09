using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Tenant.Infrastructure.Migrations
{
    /// <summary>
    /// "Prof" của monolith là học hàm học vị (GS, PGS…), không phải chuyên ngành — đổi tên bảng, giữ dữ liệu.
    /// Policy RLS gắn theo bảng nên vẫn còn sau khi đổi tên.
    /// </summary>
    public partial class RenameMajorsToAcademicTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "majors", newName: "academic_titles");
            migrationBuilder.Sql("ALTER TABLE academic_titles RENAME CONSTRAINT pk_majors TO pk_academic_titles;");
            migrationBuilder.RenameIndex(name: "ix_majors_public_id", table: "academic_titles", newName: "ix_academic_titles_public_id");
            migrationBuilder.RenameIndex(name: "ix_majors_tenant_id", table: "academic_titles", newName: "ix_academic_titles_tenant_id");
            migrationBuilder.RenameIndex(name: "ix_majors_tenant_id_name", table: "academic_titles", newName: "ix_academic_titles_tenant_id_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(name: "ix_academic_titles_tenant_id_name", table: "academic_titles", newName: "ix_majors_tenant_id_name");
            migrationBuilder.RenameIndex(name: "ix_academic_titles_tenant_id", table: "academic_titles", newName: "ix_majors_tenant_id");
            migrationBuilder.RenameIndex(name: "ix_academic_titles_public_id", table: "academic_titles", newName: "ix_majors_public_id");
            migrationBuilder.Sql("ALTER TABLE academic_titles RENAME CONSTRAINT pk_academic_titles TO pk_majors;");
            migrationBuilder.RenameTable(name: "academic_titles", newName: "majors");
        }
    }
}