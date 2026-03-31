import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { useAuth } from "../app/auth";
import { api } from "../lib/api";
import { ProductMappingsPage } from "./product-mappings-page";
import { renderWithProviders } from "../test/render-utils";

vi.mock("../app/auth", () => ({
  useAuth: vi.fn()
}));

vi.mock("../lib/api", () => ({
  api: {
    getProductMappings: vi.fn(),
    searchNobitaProducts: vi.fn(),
    syncNobitaProducts: vi.fn(),
    updateProductMapping: vi.fn()
  }
}));

const mockedUseAuth = vi.mocked(useAuth);
const mockedApi = vi.mocked(api);

describe("ProductMappingsPage", () => {
  beforeEach(() => {
    mockedUseAuth.mockReturnValue({
      authenticated: true,
      loading: false,
      csrfToken: "csrf-token",
      user: null,
      refresh: vi.fn(),
      login: vi.fn(),
      logout: vi.fn()
    });
  });

  it("renders product mappings and saves manual Nobita mapping", async () => {
    mockedApi.getProductMappings.mockResolvedValue([
      {
        id: "product-kcn",
        code: "KCN",
        name: "Kem Chống Nắng",
        basePrice: 320000,
        nobitaProductId: 101,
        nobitaWeight: 0.25,
        nobitaLastSyncedAt: "2026-03-30T00:00:00Z",
        nobitaSyncError: null
      }
    ]);
    mockedApi.searchNobitaProducts.mockResolvedValue([
      {
        productId: 555,
        code: "KCN-PRO",
        name: "Kem Chống Nắng Nobita",
        price: 320000,
        isOutOfStock: false
      }
    ]);
    mockedApi.syncNobitaProducts.mockResolvedValue([]);
    mockedApi.updateProductMapping.mockResolvedValue({
      succeeded: true,
      message: "Đã cập nhật mapping sản phẩm.",
      externalReference: null
    });

    renderWithProviders(<ProductMappingsPage />);

    expect(await screen.findByText("Kem Chống Nắng")).toBeInTheDocument();

    const productIdInput = screen.getByDisplayValue("101");
    await userEvent.clear(productIdInput);
    await userEvent.type(productIdInput, "555");

    const weightInput = screen.getByDisplayValue("0.25");
    await userEvent.clear(weightInput);
    await userEvent.type(weightInput, "0.45");

    await userEvent.click(screen.getByRole("button", { name: "Lưu" }));

    await waitFor(() => {
      expect(mockedApi.updateProductMapping).toHaveBeenCalledWith("product-kcn", 555, 0.45, "csrf-token");
    });
  });

  it("shows query error when product mappings fail to load", async () => {
    mockedApi.getProductMappings.mockRejectedValue(new Error("Không tải được catalog"));
    mockedApi.searchNobitaProducts.mockResolvedValue([]);
    mockedApi.syncNobitaProducts.mockResolvedValue([]);
    mockedApi.updateProductMapping.mockResolvedValue({
      succeeded: true,
      message: "ok",
      externalReference: null
    });

    renderWithProviders(<ProductMappingsPage />);

    expect(await screen.findByText("Không tải được catalog")).toBeInTheDocument();
  });
});
