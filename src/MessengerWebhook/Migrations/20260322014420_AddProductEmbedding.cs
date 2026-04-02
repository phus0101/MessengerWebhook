using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add vector column for pgvector (768 dimensions for text-embedding-004)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Products""
                ADD COLUMN ""Embedding"" vector(768);
            ");

            // Create IVFFlat index for fast cosine similarity search
            migrationBuilder.Sql(@"
                CREATE INDEX idx_products_embedding
                ON ""Products"" USING ivfflat (""Embedding"" vector_cosine_ops)
                WITH (lists = 100);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_products_embedding;");
            migrationBuilder.Sql(@"ALTER TABLE ""Products"" DROP COLUMN IF EXISTS ""Embedding"";");
        }
    }
}
