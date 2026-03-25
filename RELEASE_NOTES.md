# Release Notes - v0.2.5

## 🚀 New Features
- **Threshold & Alert Monitoring**: 
  - Define custom rules using math formulas (e.g., `battery_voltage < 6.5`).
  - Automatic evaluation of rules for every data point.
  - Visual diamond-shaped alert markers placed directly on signal traces in the plot.
  - New **Thresholds** section in the sidebar for easy management.
- **Violation Export**: Export all triggered alerts to a CSV file for reporting and analysis.
- **Empty State Indicators**: Added helpful messages and guidance when the Signals or Thresholds lists are empty.

## 🛠️ Improvements & Fixes
- **Unified Sidebar UI**: Standardized "Signals", "Thresholds", and "Statistics" headers with consistent colors and borders.
- **Legend Optimization**: Grouped threshold markers in the plot legend to prevent duplicate entries.
- **Release Packaging**: Fixed an issue where the `Plugins/` folder was missing from Windows release ZIPs.
- **SDK Stability**: Enhanced formula engine with robust boolean evaluation support.
