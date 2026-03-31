import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { useAuth } from "../app/auth";
import { api } from "../lib/api";
import { SupportCaseDetailPage } from "./support-case-detail-page";
import { renderWithRoute } from "../test/render-utils";

vi.mock("../app/auth", () => ({
  useAuth: vi.fn()
}));

vi.mock("../lib/api", () => ({
  api: {
    getSupportCase: vi.fn(),
    claimSupportCase: vi.fn(),
    resolveSupportCase: vi.fn(),
    cancelSupportCase: vi.fn()
  }
}));

const mockedUseAuth = vi.mocked(useAuth);
const mockedApi = vi.mocked(api);

describe("SupportCaseDetailPage", () => {
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

    mockedApi.getSupportCase.mockResolvedValue({
      id: "case-1",
      facebookPSID: "psid-1",
      facebookPageId: "PAGE_TEST_1",
      reason: "PolicyException",
      status: "Open",
      summary: "Khách xin thêm khuyến mãi",
      transcriptExcerpt: "Cho chị thêm quà với em ơi",
      assignedToEmail: "manager@test.local",
      claimedByEmail: null,
      resolvedByEmail: null,
      resolutionNotes: null,
      createdAt: "2026-03-30T00:00:00Z",
      claimedAt: null,
      resolvedAt: null,
      auditLogs: []
    });
    mockedApi.claimSupportCase.mockResolvedValue({ success: true });
    mockedApi.resolveSupportCase.mockResolvedValue({ success: true });
    mockedApi.cancelSupportCase.mockResolvedValue({ success: true });
  });

  it("renders support case detail and resolves with notes", async () => {
    renderWithRoute(<SupportCaseDetailPage />, "/support-cases/case-1", "/support-cases/:id");

    expect(await screen.findByText("Khách xin thêm khuyến mãi")).toBeInTheDocument();

    await userEvent.type(screen.getByPlaceholderText("Resolution notes"), "Đã gọi khách xác nhận.");
    await userEvent.click(screen.getByRole("button", { name: "Resolve" }));

    await waitFor(() => {
      expect(mockedApi.resolveSupportCase).toHaveBeenCalledWith("case-1", "Đã gọi khách xác nhận.", "csrf-token");
    });
  });
});
