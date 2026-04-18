import { useState } from "react";
import { subDays, format } from "date-fns";
import type { DateRange, DateRangePreset } from "../../types/metrics";

interface DateRangePickerProps {
  value: DateRange;
  onChange: (range: DateRange) => void;
}

export function DateRangePicker({ value, onChange }: DateRangePickerProps) {
  const [preset, setPreset] = useState<DateRangePreset>('7d');
  const [showCustom, setShowCustom] = useState(false);

  const handlePresetChange = (newPreset: DateRangePreset) => {
    setPreset(newPreset);
    setShowCustom(newPreset === 'custom');

    if (newPreset !== 'custom') {
      const endDate = new Date();
      let startDate: Date;

      switch (newPreset) {
        case '7d':
          startDate = subDays(endDate, 7);
          break;
        case '14d':
          startDate = subDays(endDate, 14);
          break;
        case '30d':
          startDate = subDays(endDate, 30);
          break;
        default:
          return;
      }

      onChange({ startDate, endDate });
    }
  };

  const handleCustomDateChange = (field: 'startDate' | 'endDate', dateString: string) => {
    const newDate = new Date(dateString);
    if (isNaN(newDate.getTime())) return;

    onChange({
      ...value,
      [field]: newDate
    });
  };

  return (
    <div className="date-range-picker">
      <div className="preset-buttons">
        <button
          type="button"
          className={`preset-btn ${preset === '7d' ? 'active' : ''}`}
          onClick={() => handlePresetChange('7d')}
        >
          7 ngày
        </button>
        <button
          type="button"
          className={`preset-btn ${preset === '14d' ? 'active' : ''}`}
          onClick={() => handlePresetChange('14d')}
        >
          14 ngày
        </button>
        <button
          type="button"
          className={`preset-btn ${preset === '30d' ? 'active' : ''}`}
          onClick={() => handlePresetChange('30d')}
        >
          30 ngày
        </button>
        <button
          type="button"
          className={`preset-btn ${preset === 'custom' ? 'active' : ''}`}
          onClick={() => handlePresetChange('custom')}
        >
          Tùy chỉnh
        </button>
      </div>

      {showCustom && (
        <div className="custom-date-inputs">
          <div className="date-input-group">
            <label htmlFor="start-date">Từ ngày:</label>
            <input
              id="start-date"
              type="date"
              value={format(value.startDate, 'yyyy-MM-dd')}
              onChange={(e) => handleCustomDateChange('startDate', e.target.value)}
              max={format(value.endDate, 'yyyy-MM-dd')}
            />
          </div>
          <div className="date-input-group">
            <label htmlFor="end-date">Đến ngày:</label>
            <input
              id="end-date"
              type="date"
              value={format(value.endDate, 'yyyy-MM-dd')}
              onChange={(e) => handleCustomDateChange('endDate', e.target.value)}
              min={format(value.startDate, 'yyyy-MM-dd')}
              max={format(new Date(), 'yyyy-MM-dd')}
            />
          </div>
        </div>
      )}
    </div>
  );
}
