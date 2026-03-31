import { screen } from "@testing-library/react";
import { vi } from "vitest";
import { api } from "../lib/api";
import { DraftOrdersPage } from "./draft-orders-page";
import { renderWithProviders } from "../test/render-utils";

vi.mock("../lib/api", () => ({
  api: {
    getDraftOrders: vi.fn()
  }
}));

const mockedApi = vi.mocked(api);

describe("DraftOrdersPage", () => {
  beforeEach(() => {
    mockedApi.getDraftOrders.mockResolvedValue([
      {
        id: "draft-1",
        draftCode: "DR-001",
        facebookPageId: "PAGE_1",
        customerName: "Khach test",
        customerPhone: "0900000001",
        shippingAddress: "1 Nguyen Hue",
        status: "PendingReview",
        riskLevel: "Low",
        requiresManualReview: true,
        assignedManagerEmail: "manager@test.local",
        itemCount: 1,
        grandTotal: 320000,
        createdAt: "2026-03-30T00:00:00Z"
      }
    ]);
  });

  it("renders draft rows without crashing on status pills", async () => {
    renderWithProviders(<DraftOrdersPage />);

    expect(await screen.findByText("DR-001")).toBeInTheDocument();
    expect(screen.getByText("Pending review")).toBeInTheDocument();
    expect(screen.getByText("Low")).toBeInTheDocument();
  });
});
