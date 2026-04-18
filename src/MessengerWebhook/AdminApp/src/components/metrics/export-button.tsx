import { exportToCSV } from "../../lib/csv-export";

interface ExportButtonProps {
  data: Record<string, unknown>[];
  filename: string;
  disabled?: boolean;
}

export function ExportButton({ data, filename, disabled = false }: ExportButtonProps) {
  const handleExport = () => {
    try {
      if (data.length === 0) {
        alert('Không có dữ liệu để xuất');
        return;
      }

      const timestamp = new Date().toISOString().split('T')[0];
      const fullFilename = `${filename}_${timestamp}.csv`;

      exportToCSV(data, fullFilename);
    } catch (error) {
      console.error('Export failed:', error);
      alert('Xuất CSV thất bại. Vui lòng thử lại.');
    }
  };

  return (
    <button
      type="button"
      className="export-button"
      onClick={handleExport}
      disabled={disabled || data.length === 0}
    >
      Xuất CSV
    </button>
  );
}
