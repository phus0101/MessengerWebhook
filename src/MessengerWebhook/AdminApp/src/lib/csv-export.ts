export function exportToCSV(data: Record<string, unknown>[], filename: string): void {
  if (data.length === 0) {
    throw new Error("No data to export");
  }

  const headers = Object.keys(data[0]);
  const csvRows: string[] = [];

  // Add header row
  csvRows.push(headers.join(','));

  // Add data rows
  for (const row of data) {
    const values = headers.map(header => {
      const value = row[header];
      const escaped = String(value ?? '').replace(/"/g, '""');
      return `"${escaped}"`;
    });
    csvRows.push(values.join(','));
  }

  const csv = csvRows.join('\n');
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.style.display = 'none';

  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);

  URL.revokeObjectURL(url);
}

export function formatMetricsForExport(data: Record<string, unknown>[], prefix: string): Record<string, unknown>[] {
  return data.map(item => {
    const formatted: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(item)) {
      formatted[`${prefix}_${key}`] = value;
    }
    return formatted;
  });
}
