import { render, screen } from "@testing-library/react";
import { StatusPill } from "./status-pill";

describe("StatusPill", () => {
  it("renders expected label and tone for string enums", () => {
    render(<StatusPill kind="draft-status" value="PendingReview" />);

    const pill = screen.getByText("Pending review");
    expect(pill).toHaveClass("status-pill--pendingreview");
  });

  it("does not crash when legacy numeric enum values are passed", () => {
    render(<StatusPill kind="risk-level" value={1} />);

    const pill = screen.getByText("Medium");
    expect(pill).toHaveClass("status-pill--medium");
  });

  it("falls back safely for unknown values", () => {
    render(<StatusPill kind="support-status" value={null} />);

    const pill = screen.getByText("Unknown");
    expect(pill).toHaveClass("status-pill--unknown");
  });
});
