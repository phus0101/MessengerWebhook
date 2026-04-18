import { screen, waitFor } from "@testing-library/react";
import { fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { useAuth } from "../app/auth";
import { api } from "../lib/api";
import { DraftOrderDetailPage } from "./draft-order-detail-page";
import { renderWithRoute } from "../test/render-utils";

vi.mock("../app/auth", () => ({
  useAuth: vi.fn()
}));

vi.mock("../lib/api", () => ({
  api: {
    getDraftOrder: vi.fn(),
    searchCustomers: vi.fn(),
    updateDraft: vi.fn(),
    approveSubmit: vi.fn(),
    retrySubmit: vi.fn(),
    rejectDraft: vi.fn()
  }
}));

const mockedUseAuth = vi.mocked(useAuth);
const mockedApi = vi.mocked(api);

describe("DraftOrderDetailPage", () => {
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

    mockedApi.getDraftOrder.mockResolvedValue({
      id: "draft-1",
      draftCode: "DR-001",
      facebookPageId: "PAGE_TEST_1",
      customerIdentityId: "customer-1",
      customerName: "Khach test",
      customerPhone: "0900000001",
      shippingAddress: "1 Nguyen Hue",
      status: "PendingReview",
      riskLevel: "Low",
      riskSummary: null,
      requiresManualReview: false,
      merchandiseTotal: 320000,
      shippingFee: 0,
      grandTotal: 320000,
      priceConfirmed: true,
      promotionConfirmed: false,
      shippingConfirmed: true,
      inventoryConfirmed: false,
      assignedManagerEmail: "manager@test.local",
      nobitaOrderId: null,
      lastSubmissionError: null,
      createdAt: "2026-03-30T00:00:00Z",
      reviewedAt: null,
      reviewedByEmail: null,
      submittedAt: null,
      submittedByEmail: null,
      isEditable: true,
      linkedCustomer: {
        customerIdentityId: "customer-1",
        fullName: "Khach test",
        phoneNumber: "0900000001",
        shippingAddress: "1 Nguyen Hue",
        totalOrders: 3,
        successfulDeliveries: 3,
        failedDeliveries: 0,
        lastInteractionAt: "2026-03-30T00:00:00Z"
      },
      items: [
        {
          id: "item-1",
          productCode: "KCN",
          productName: "Kem Chong Nang",
          quantity: 1,
          unitPrice: 320000,
          giftCode: "GIFT_KCN",
          giftName: "Mat na duong sang"
        }
      ],
      availableProducts: [
        {
          code: "KCN",
          name: "Kem Chong Nang",
          unitPrice: 320000,
          giftOptions: [{ code: "GIFT_KCN", name: "Mat na duong sang" }]
        },
        {
          code: "KL",
          name: "Kem Lua",
          unitPrice: 410000,
          giftOptions: [{ code: "GIFT_KL", name: "Tinh chat mini" }]
        }
      ],
      auditLogs: []
    });

    mockedApi.searchCustomers.mockResolvedValue([
      {
        customerIdentityId: "customer-2",
        fullName: "Khach cu VIP",
        phoneNumber: "0911111111",
        shippingAddress: "22 Ly Tu Trong",
        totalOrders: 12,
        successfulDeliveries: 11,
        failedDeliveries: 1,
        lastInteractionAt: "2026-03-29T00:00:00Z"
      }
    ]);
    mockedApi.updateDraft.mockResolvedValue({ succeeded: true, message: "Da luu thay doi don nhap." });
    mockedApi.approveSubmit.mockResolvedValue({ succeeded: true, message: "ok" });
    mockedApi.retrySubmit.mockResolvedValue({ succeeded: true, message: "ok" });
    mockedApi.rejectDraft.mockResolvedValue({ succeeded: true, message: "ok" });
  });

  it("renders editable form and saves updated draft payload", async () => {
    renderWithRoute(<DraftOrderDetailPage />, "/draft-orders/draft-1", "/draft-orders/:id");

    expect(await screen.findByDisplayValue("Khach test")).toBeInTheDocument();

    await userEvent.clear(screen.getByDisplayValue("Khach test"));
    await userEvent.type(screen.getByLabelText("Ten khach"), "Khach moi");
    await userEvent.clear(screen.getByLabelText("So dien thoai"));
    await userEvent.type(screen.getByLabelText("So dien thoai"), "0988888888");
    await userEvent.clear(screen.getByLabelText("Dia chi giao hang"));
    await userEvent.type(screen.getByLabelText("Dia chi giao hang"), "2 Le Loi");

    await userEvent.click(screen.getByRole("button", { name: "Them dong" }));

    const productSelectors = screen.getAllByLabelText("San pham");

    await userEvent.selectOptions(productSelectors[1], "KL");
    fireEvent.change(screen.getAllByLabelText("So luong")[1], { target: { value: "2" } });
    await waitFor(() => {
      expect(screen.getAllByLabelText("Qua tang")[1]).toHaveValue("GIFT_KL");
    });

    await userEvent.click(screen.getByRole("button", { name: "Luu thay doi" }));

    await waitFor(() => {
      expect(mockedApi.updateDraft).toHaveBeenCalledWith("draft-1", {
        customerIdentityId: null,
        customerName: "Khach moi",
        customerPhone: "0988888888",
        shippingAddress: "2 Le Loi",
        items: [
          { productCode: "KCN", quantity: 1, giftCode: "GIFT_KCN" },
          { productCode: "KL", quantity: 2, giftCode: "GIFT_KL" }
        ]
      }, "csrf-token");
    });
  });

  it("prefills from an existing customer and still allows manual edits", async () => {
    renderWithRoute(<DraftOrderDetailPage />, "/draft-orders/draft-1", "/draft-orders/:id");

    expect(await screen.findByDisplayValue("Khach test")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("Tim khach hang co san"), "VIP");

    expect(await screen.findByText("Khach cu VIP - 0911111111")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Chon khach nay" }));

    await waitFor(() => {
      expect(screen.getByLabelText("Ten khach")).toHaveValue("Khach cu VIP");
      expect(screen.getByLabelText("So dien thoai")).toHaveValue("0911111111");
      expect(screen.getByLabelText("Dia chi giao hang")).toHaveValue("22 Ly Tu Trong");
      expect(screen.getByText("Da nap thong tin vao form. Ban van co the chinh sua truoc khi luu.")).toBeInTheDocument();
      expect(screen.getByLabelText("Ten khach")).not.toBeDisabled();
    });

    await userEvent.clear(screen.getByLabelText("So dien thoai"));
    await userEvent.type(screen.getByLabelText("So dien thoai"), "0933333333");
    await userEvent.clear(screen.getByLabelText("Dia chi giao hang"));
    await userEvent.type(screen.getByLabelText("Dia chi giao hang"), "55 Pasteur");

    await userEvent.click(screen.getByRole("button", { name: "Luu thay doi" }));

    await waitFor(() => {
      expect(mockedApi.updateDraft).toHaveBeenCalledWith("draft-1", {
        customerIdentityId: "customer-2",
        customerName: "Khach cu VIP",
        customerPhone: "0933333333",
        shippingAddress: "55 Pasteur",
        items: [
          { productCode: "KCN", quantity: 1, giftCode: "GIFT_KCN" }
        ]
      }, "csrf-token");
    });
  });

  it("shows provisional total label when shipping is not confirmed", async () => {
    mockedApi.getDraftOrder.mockResolvedValueOnce({
      id: "draft-1",
      draftCode: "DR-001",
      facebookPageId: "PAGE_TEST_1",
      customerIdentityId: "customer-1",
      customerName: "Khach test",
      customerPhone: "0900000001",
      shippingAddress: "1 Nguyen Hue",
      status: "PendingReview",
      riskLevel: "Low",
      riskSummary: null,
      requiresManualReview: false,
      merchandiseTotal: 320000,
      shippingFee: 30000,
      grandTotal: 350000,
      priceConfirmed: true,
      promotionConfirmed: false,
      shippingConfirmed: false,
      inventoryConfirmed: false,
      assignedManagerEmail: "manager@test.local",
      nobitaOrderId: null,
      lastSubmissionError: null,
      createdAt: "2026-03-30T00:00:00Z",
      reviewedAt: null,
      reviewedByEmail: null,
      submittedAt: null,
      submittedByEmail: null,
      isEditable: true,
      linkedCustomer: {
        customerIdentityId: "customer-1",
        fullName: "Khach test",
        phoneNumber: "0900000001",
        shippingAddress: "1 Nguyen Hue",
        totalOrders: 3,
        successfulDeliveries: 3,
        failedDeliveries: 0,
        lastInteractionAt: "2026-03-30T00:00:00Z"
      },
      items: [
        {
          id: "item-1",
          productCode: "KCN",
          productName: "Kem Chong Nang",
          quantity: 1,
          unitPrice: 320000,
          giftCode: "GIFT_KCN",
          giftName: "Mat na duong sang"
        }
      ],
      availableProducts: [
        {
          code: "KCN",
          name: "Kem Chong Nang",
          unitPrice: 320000,
          giftOptions: [{ code: "GIFT_KCN", name: "Mat na duong sang" }]
        }
      ],
      auditLogs: []
    });

    renderWithRoute(<DraftOrderDetailPage />, "/draft-orders/draft-1", "/draft-orders/:id");

    expect(await screen.findByText(/tạm tính/i)).toBeInTheDocument();
  });
});
