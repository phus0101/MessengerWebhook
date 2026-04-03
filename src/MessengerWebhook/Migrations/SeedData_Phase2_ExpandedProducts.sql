-- Seed data Phase 2: Expanded product catalog
-- Run this script to add more sample products for testing

-- Insert expanded product catalog
INSERT INTO "Products" ("Id", "Code", "Name", "Description", "Brand", "Category", "BasePrice", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
    -- Existing products (update if exists)
    (gen_random_uuid(), 'KCN', 'Kem Chống Nắng SPF50+', 'Kem chống nắng phổ rộng, bảo vệ da khỏi tia UVA/UVB, phù hợp mọi loại da', 'Múi Xù', 0, 350000, true, NOW(), NOW()),
    (gen_random_uuid(), 'KL', 'Kem Lụa Dưỡng Ẩm', 'Kem dưỡng ẩm mịn như lụa, cấp nước sâu cho da, giúp da mềm mại suốt ngày', 'Múi Xù', 0, 280000, true, NOW(), NOW()),
    (gen_random_uuid(), 'COMBO_2', 'Combo 2 Sản Phẩm', 'Combo 2 sản phẩm bất kỳ - Miễn phí vận chuyển toàn quốc', 'Múi Xù', 0, 600000, true, NOW(), NOW()),

    -- New products for expanded catalog
    (gen_random_uuid(), 'KTN', 'Kem Trị Nám Tàn Nhang', 'Kem đặc trị nám, tàn nhang, đốm nâu. Công thức Vitamin C + Niacinamide giúp làm mờ vết thâm hiệu quả sau 4 tuần', 'Múi Xù', 0, 420000, true, NOW(), NOW()),
    (gen_random_uuid(), 'SRM', 'Sữa Rửa Mặt Tạo Bọt', 'Sữa rửa mặt tạo bọt mịn, làm sạch sâu lỗ chân lông, không gây khô da', 'Múi Xù', 0, 180000, true, NOW(), NOW()),
    (gen_random_uuid(), 'TN', 'Toner Cân Bằng Da', 'Toner cân bằng pH, se khít lỗ chân lông, cấp ẩm tức thì cho da', 'Múi Xù', 0, 220000, true, NOW(), NOW()),
    (gen_random_uuid(), 'SR', 'Serum Vitamin C', 'Serum Vitamin C 20% nguyên chất, làm sáng da, mờ thâm nám, chống lão hóa', 'Múi Xù', 0, 480000, true, NOW(), NOW()),
    (gen_random_uuid(), 'MN', 'Mặt Nạ Ngủ Dưỡng Ẩm', 'Mặt nạ ngủ cấp ẩm chuyên sâu, phục hồi da qua đêm, da sáng mịn khi thức dậy', 'Múi Xù', 0, 320000, true, NOW(), NOW()),
    (gen_random_uuid(), 'KDM', 'Kem Dưỡng Mắt', 'Kem dưỡng vùng mắt, giảm quầng thâm, bọng mắt, nếp nhăn vùng mắt', 'Múi Xù', 0, 380000, true, NOW(), NOW()),
    (gen_random_uuid(), 'COMBO_3', 'Combo Trị Nám Toàn Diện', 'Combo 3 sản phẩm: Kem Trị Nám + Serum Vitamin C + Kem Chống Nắng. Giảm 15% + Freeship', 'Múi Xù', 0, 1050000, true, NOW(), NOW())
ON CONFLICT ("Code") DO UPDATE SET
    "Name" = EXCLUDED."Name",
    "Description" = EXCLUDED."Description",
    "BasePrice" = EXCLUDED."BasePrice",
    "UpdatedAt" = NOW();

-- Insert additional gifts
INSERT INTO "Gifts" ("Id", "Code", "Name", "Description", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
    (gen_random_uuid(), 'GIFT_SRM_MINI', 'Sữa rửa mặt mini 50ml', 'Làm sạch sâu, dịu nhẹ cho mọi loại da', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_TONER_MINI', 'Toner cân bằng mini 30ml', 'Cân bằng pH da, se khít lỗ chân lông', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_MASK', 'Mặt nạ dưỡng da', 'Cấp ẩm chuyên sâu, làm dịu da', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_SERUM_SAMPLE', 'Serum dưỡng da sample 5ml', 'Dưỡng trắng, chống lão hóa', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_LIPBALM', 'Son dưỡng môi SPF15', 'Dưỡng ẩm, chống nắng cho môi', true, NOW(), NOW()),
    (gen_random_uuid(), 'GIFT_COTTON_PAD', 'Bông tẩy trang 80 miếng', 'Bông tẩy trang mềm mại, không gây kích ứng', true, NOW(), NOW())
ON CONFLICT DO NOTHING;

-- Update product-gift mappings
INSERT INTO "ProductGiftMappings" ("Id", "ProductCode", "GiftCode", "Priority", "CreatedAt")
VALUES
    -- Existing mappings
    (gen_random_uuid(), 'KCN', 'GIFT_SRM_MINI', 1, NOW()),
    (gen_random_uuid(), 'KCN', 'GIFT_TONER_MINI', 2, NOW()),
    (gen_random_uuid(), 'KL', 'GIFT_MASK', 1, NOW()),
    (gen_random_uuid(), 'KL', 'GIFT_SERUM_SAMPLE', 2, NOW()),
    (gen_random_uuid(), 'COMBO_2', 'GIFT_LIPBALM', 1, NOW()),

    -- New mappings
    (gen_random_uuid(), 'KTN', 'GIFT_SERUM_SAMPLE', 1, NOW()),
    (gen_random_uuid(), 'KTN', 'GIFT_COTTON_PAD', 2, NOW()),
    (gen_random_uuid(), 'SRM', 'GIFT_TONER_MINI', 1, NOW()),
    (gen_random_uuid(), 'TN', 'GIFT_COTTON_PAD', 1, NOW()),
    (gen_random_uuid(), 'SR', 'GIFT_MASK', 1, NOW()),
    (gen_random_uuid(), 'MN', 'GIFT_SERUM_SAMPLE', 1, NOW()),
    (gen_random_uuid(), 'KDM', 'GIFT_MASK', 1, NOW()),
    (gen_random_uuid(), 'COMBO_3', 'GIFT_LIPBALM', 1, NOW())
ON CONFLICT DO NOTHING;
