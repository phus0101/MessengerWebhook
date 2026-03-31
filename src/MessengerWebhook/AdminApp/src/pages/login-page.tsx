import { FormEvent, useState } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../app/auth";

export function LoginPage() {
  const { authenticated, login, loading } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!loading && authenticated) {
    return <Navigate replace to="/" />;
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await login(email, password, true);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Đăng nhập thất bại.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="screen-center">
      <form className="auth-card" onSubmit={handleSubmit}>
        <span className="eyebrow">Internal admin</span>
        <h1>Đăng nhập vận hành</h1>
        <p>Quản lý đơn nháp, support case và gửi Nobita từ một chỗ.</p>
        <label>
          Email
          <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" required />
        </label>
        <label>
          Mật khẩu
          <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" required />
        </label>
        {error ? <div className="error-box">{error}</div> : null}
        <button className="primary-button" disabled={submitting} type="submit">
          {submitting ? "Đang đăng nhập..." : "Đăng nhập"}
        </button>
      </form>
    </div>
  );
}
