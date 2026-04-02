-- Seed data for Phase 1: Quick Reply Handler
-- Run this script after applying migration AddGiftsAndProductCode

-- Insert sample products with codes
INSERT INTO "Products" ("Id", "Code", "Name", "Description", "Brand", "Category", "BasePrice", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
    (gen_random_uuid(), 'KCN', 'Kem Chống Nắng SPF50+', 'Kem chống nắng phổ rộng, bảo vệ da khỏi tia UVA/UVB', 'Múi Xù', 0, 350000, true, NOW(), NOW()),
    (gen_random_uuid(), 'KL', 'Kem Lụa Dưỡng Ẩm', 'Kem dưỡng ẩm mịn như lụa, cấp nước cho da', 'Múi Xù', 0, 280000, true, NOW(), NOW()),
    (gen_random_uuid(), 'COMBO_2', 'Combo 2 Sản Phẩm', 'Combo 2 sản phẩm bất kỳ - Miễn phí vận chuyển', 'Múi Xù', 0, 600000, true, NOW(), NOW())
ON CONFLICT ("Code") DO NOTHING;

-- Insert sample gifts
INSERT INTO "Gifts" ("Id", "Code", "Name", "Description", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
    (gen_random_uuid(), 'GIFT_SRM_MINI', 'Sữa rửa mặt mini 50ml', 'Làm sạch sâu, dịu nhẹ cho mọi loại da', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_TONER_MINI', 'Toner cân bằng mini 30ml', 'Cân bằng pH da, se khít lỗ chân lông', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_MASK', 'Mặt nạ dưỡng da', 'Cấp ẩm chuyên sâu, làm dịu da', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_SERUM_SAMPLE', 'Serum dưỡng da sample 5ml', 'Dưỡng trắng, chống lão hóa', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_LIPBALM', 'Son dưỡng môi SPF15', 'Dưỡng ẩm, chống nắng cho môi', true, NOW(), NOW())
ON CONFLICT ("Code") DO NOTHING;

-- Insert product-gift mappings
INSERT INTO "ProductGiftMappings" ("Id", "ProductCode", "GiftCode", "Priority", "CreatedAt")
VALUES
    (gen_random_uuid(), 'KCN', 'GIFT_SRM_MINI', 1, NOW()),
    (gen_random_uuid(), 'KCN', 'GIFT_TONER_MINI', 2, NOW()),
    (gen_random_uuid(), 'KL', 'GIFT_MASK', 1, NOW()),
    (gen_random_uuid(), 'KL', 'GIFT_SERUM_SAMPLE', 2, NOW()),
    (gen_random_uuid(), 'COMBO_2', 'GIFT_LIPBALM', 1, NOW())
ON CONFLICT ("ProductCode", "GiftCode") DO NOTHING;
