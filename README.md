# SignalBench

**A telemetry analysis workbench suitable for real test campaigns.**

SignalBench is the cleanest binary telemetry decoding desktop tool a CubeSat engineer can install and use within 5 minutes. It is a high-performance, engineer-grade telemetry workbench designed for aerospace test and telemetry engineers that supports CSV, binary logs, and live serial streams. Decode, visualize, and analyze telemetry without the need for custom scripting.

## 🚀 Features

- **Multiple Format Support**: Load and decode CSV files or binary telemetry using YAML-defined packet schemas
- **Live Serial Streaming**: Connect to COM ports to decode and visualize data in real-time
- **High Performance**: Handles large files (> 500K+ records) and high-frequency streams efficiently
- **Visualization**: Interactive plots with "Oscilloscope" (Fill-then-Roll) effect for live data
- **Derived Signals**: Create custom calculated signals using math expressions (e.g., `sqrt(battery_voltage)`)
- **Data Logging**: Record raw serial streams directly to disk while visualizing
- **Session Management**: Save and restore workspace sessions

## 🚀 Getting Started (Quick Start)

### 1. Load a Packet Schema
Before loading data or streaming, the app needs to know the structure of the packets.
*   Click **"Open Schema"** in the **SCHEMA** group.
*   Select a `.yaml` schema file.

### 2. Live Serial Streaming
*   **Configure**: Click the **Gear icon** in the **SERIAL** group.
    *   Select your **COM Port**, **Baud Rate**, and **Serial Parameters**.
    *   Set the **Rolling Window Size** (e.g., 1000) to control the live plot width.
*   **Start**: Click the **Play icon** in the **SERIAL** group.
    *   The plot will fill from left-to-right and then "roll" forward as new data arrives.
*   **Record**: Click the **Record icon** (Red Circle) while streaming to save raw binary data to a file.

### 3. Static Telemetry Data
*   Click **"Open Telemetry"** in the **DATA FILE** group.
    *   **CSV**: Detects headers and loads data automatically.
    *   **Binary**: Uses the selected schema to decode the file.
*   *Note: Switching between serial streaming and static files will reset the workspace to ensure data integrity.*

### 4. Navigation & Playback
*   Use the **Timeline Slider** and playback buttons (Play, Fast Forward/Backward, Step) to navigate captured data.
*   A high-precision timestamp display shows the exact date and time (with milliseconds) of the current frame.

## 📋 Requirements & Setup

- **Platform**: Windows, Linux, macOS (Cross-platform via Avalonia)
- **Runtime**: .NET 9.0 SDK
- **Hardware**: Serial port access (Physical or Virtual via `com0com`/`VSPE`)
- **Dependencies**: 
  - Avalonia UI
  - ScottPlot (v5.1+)
  - System.IO.Ports
  - YamlDotNet
  - Microsoft.Data.Sqlite
  - NCalcSync

### Building from Source

```bash
dotnet build SignalBench.sln
```

### Running Tests

```bash
dotnet test
```

## 🛠 Simulation & Testing

To test serial streaming without physical hardware:
1.  Setup a virtual serial pair (e.g., `COM1` <-> `COM2`).
2.  Run the included simulation script: `powershell -File simulate.ps1` (streams to `COM1`).
3.  In SignalBench, load `sim_schema.yaml` and connect to `COM2`.

---

*Built for engineers who need results, not fluff.*
