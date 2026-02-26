# SignalBench

**A telemetry analysis workbench suitable for real test campaigns.**

SignalBench is the cleanest binary telemetry decoding desktop tool a CubeSat engineer can install and use within 5 minutes. It is a high-performance, engineer-grade telemetry workbench designed for aerospace test and telemetry engineers that supports both CSV and binary telemetry formats. Decode, visualize, and analyze raw telemetry logs without the need for custom scripting.

## 🚀 Features

- **Multiple Format Support**: Load and decode CSV files or binary telemetry using YAML-defined packet schemas
- **High Performance**: Handles large files with millions of records efficiently
- **Visualization**: Interactive plots with zoom, pan, and signal selection
- **Derived Signals**: Create custom calculated signals using math expressions (e.g., `sqrt(battery_voltage)`, `temperature_1 - temperature_2`)
- **Session Management**: Save and restore workspace sessions

## 🚀 Getting Started (Quick Start)

To decode and visualize your telemetry data, follow these steps:

### 1. Load a Packet Schema
Before loading binary data, the app needs to know the structure of the packets.
*   Click **"Load Schema"** in the top toolbar.
*   Select a `.yaml` schema file. (See `Samples/eps_telemetry_schema.yaml`)
*   Select your loaded schema from the **"SCHEMA"** dropdown in the left panel.

### 2. Open Telemetry Data
*   Click **"Open Telemetry"** in the toolbar.
*   **For CSV files**: The app automatically detects headers and loads the data into the preview table.
*   **For Binary files**: The app uses your selected schema to scan and decode the file into structured records.

### 3. Visualize Signals
*   Once data is loaded, look at the **"SIGNALS"** list in the left panel.
*   **Check the box** next to any signal (e.g., `battery_voltage`) to plot it in the center area.
*   **Interact with the plot**:
    *   **Left-click + Drag**: Pan the view.
    *   **Right-click + Drag**: Zoom in/out.
    *   **Middle-click / Scroll**: Zoom.
    *   **Double-click**: Reset the view to fit all data.

### 4. Data Preview
*   The **"DATA PREVIEW"** panel at the bottom shows the first 100 decoded records in a spreadsheet format for quick verification.

### 5. Export and Save
*   **Export CSV**: Click this to save the currently decoded dataset to a CSV file.
*   **Save Session**: Click this to save your current setup as a `.sbx` project file.

## 📋 Requirements & Setup

- **Platform**: Windows, Linux, macOS (Cross-platform via Avalonia)
- **Runtime**: .NET 9.0 SDK
- **Dependencies**: 
  - Avalonia UI (v11.3.12)
  - ScottPlot (v5.1+)
  - YamlDotNet
  - Microsoft.Data.Sqlite
  - NCalcSync

### Building from Source

```bash
cd SignalBench
dotnet build
```

### Running Tests

```bash
dotnet test
```

## 🛠 Architecture

SignalBench is built with a strictly decoupled architecture:
- **SignalBench.Core**: UI-independent logic (decoding, schema, data persistence).
- **SignalBench (UI)**: Desktop application built with Avalonia UI and ReactiveUI.
- **SignalBench.Tests**: xUnit test suite.

---

*Built for engineers who need results, not fluff.*
