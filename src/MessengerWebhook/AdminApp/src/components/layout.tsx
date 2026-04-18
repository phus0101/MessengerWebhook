import { Link, NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../app/auth";

const navItems = [
  { to: "/", label: "Tổng quan" },
  { to: "/draft-orders", label: "Đơn nháp" },
  { to: "/support-cases", label: "Support cases" },
  { to: "/product-mappings", label: "Product mapping" },
  { to: "/vector-search", label: "Vector Search" },
  { to: "/metrics", label: "A/B Testing Metrics" }
];

export function AdminLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  return (
    <div className="shell">
      <aside className="sidebar">
        <Link className="brand" to="/">
          Messenger Sales Admin
        </Link>
        <nav className="nav">
          {navItems.map((item) => (
            <NavLink key={item.to} className="nav-link" to={item.to} end={item.to === "/"}>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-footer">
          <div className="user-card">
            <strong>{user?.fullName}</strong>
            <span>{user?.email}</span>
          </div>
          <button
            className="ghost-button"
            onClick={async () => {
              await logout();
              navigate("/login");
            }}
            type="button"
          >
            Đăng xuất
          </button>
        </div>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
