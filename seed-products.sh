#!/bin/bash

# Seed expanded product catalog to PostgreSQL in Docker

echo "Seeding expanded product catalog..."

docker exec -i messenger_bot_postgres psql -U postgres -d messenger_bot < src/MessengerWebhook/Migrations/SeedData_Phase2_ExpandedProducts.sql

if [ $? -eq 0 ]; then
    echo "✓ Seed data inserted successfully!"
    echo ""
    echo "Next steps:"
    echo "1. Start app: dotnet run --project src/MessengerWebhook"
    echo "2. Go to Admin Dashboard"
    echo "3. Run indexing to make products searchable via RAG"
else
    echo "✗ Failed to seed data. Check if Docker container is running."
    echo "Run: docker ps | grep postgres"
fi
