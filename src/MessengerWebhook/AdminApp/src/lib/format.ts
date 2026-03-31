const currencyFormatter = new Intl.NumberFormat("vi-VN", {
  style: "currency",
  currency: "VND",
  maximumFractionDigits: 0
});

export function formatMoney(value: number) {
  return currencyFormatter.format(value);
}

export function formatDate(value?: string | null) {
  if (!value) {
    return "N/A";
  }

  return new Date(value).toLocaleString("vi-VN");
}
