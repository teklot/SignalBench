# Release Notes - v0.3.1

## 🛠️ Fixes & Improvements
- Fixed streaming plot freeze when adding or editing thresholds during live data
- Fixed session loading crash when derived signals are not in the schema
- Threshold markers now appear across the entire visible rolling window during streaming
- Streaming plot now uses Task Manager-style display with newest data anchored to the right
- X-axis shows time-only format (HH:mm:ss.fff) during streaming

---

# Release Notes - v0.3.0

## 🚀 New Features (SignalFlux Integration)
- **Unified Binary Protocol Platform**: SignalFlux is now a core component of the SignalBench ecosystem.
- **Headless Signal Generation**: New `signalflux` CLI for high-performance, headless protocol simulation.
- **Unified Schema Engine**: Use the exact same YAML packet schemas for both generation (Flux) and analysis (Bench).
- **Generic Schema Encoder**: New schema-driven encoding engine that replaces hardcoded protocol logic. Supports any bit-aligned binary format defined in YAML.
- **Inline Simulation**: Support for quick protocol testing via CLI without configuration files.

# Release Notes - v0.2.6

## 🚀 New Features
- **CRC Validation**: 
  - Full support for CRC-8, CRC-16, and CRC-32 (Cyclic Redundancy Checks) for binary packets.
  - Automatic validation of every received packet against the schema-defined CRC.
  - Visual **red "X" markers** on the signal graph traces for all data points that fail integrity checks.
  - High-performance CRC engine with support for bit-reflection (input/output) and custom polynomials.
- **Enhanced Delimited File Support**: 
  - Added native support for **.tsv**, **.txt**, and **.log** files alongside CSV.
  - Improved the import wizard to handle various delimiters (comma, semicolon, pipe, tab).
  - Explicitly **skips the timestamp signal** from the sidebar and plot to keep the focus on telemetry data.

## 🛠️ Improvements & Fixes
- **Optimized Session Management**: 
  - Session files (`.sbs`) now store **external schema references** instead of embedding full schema YAML, reducing file size and ensuring consistent schema management.
  - Automatically identifies delimited files in sessions to skip unnecessary binary schema logic.
- **CRC Positioning**: Improved binary decoding to support CRC fields located at any position within the packet (beginning, middle, or end).

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
