import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { useAuth } from "../app/auth";
import { LoginPage } from "./login-page";
import { renderWithProviders } from "../test/render-utils";

vi.mock("../app/auth", () => ({
  useAuth: vi.fn()
}));

const mockedUseAuth = vi.mocked(useAuth);

describe("LoginPage", () => {
  it("submits credentials through admin auth", async () => {
    const login = vi.fn().mockResolvedValue(undefined);
    mockedUseAuth.mockReturnValue({
      authenticated: false,
      loading: false,
      csrfToken: "csrf-token",
      user: null,
      refresh: vi.fn(),
      login,
      logout: vi.fn()
    });

    renderWithProviders(<LoginPage />);

    await userEvent.type(screen.getByLabelText("Email"), "ops@test.local");
    await userEvent.type(screen.getByLabelText("Mật khẩu"), "secret123");
    await userEvent.click(screen.getByRole("button", { name: "Đăng nhập" }));

    await waitFor(() => {
      expect(login).toHaveBeenCalledWith("ops@test.local", "secret123", true);
    });
  });

  it("shows inline error when login fails", async () => {
    const login = vi.fn().mockRejectedValue(new Error("Sai mật khẩu"));
    mockedUseAuth.mockReturnValue({
      authenticated: false,
      loading: false,
      csrfToken: "csrf-token",
      user: null,
      refresh: vi.fn(),
      login,
      logout: vi.fn()
    });

    renderWithProviders(<LoginPage />);

    await userEvent.type(screen.getByLabelText("Email"), "ops@test.local");
    await userEvent.type(screen.getByLabelText("Mật khẩu"), "wrong-password");
    await userEvent.click(screen.getByRole("button", { name: "Đăng nhập" }));

    expect(await screen.findByText("Sai mật khẩu")).toBeInTheDocument();
  });
});
