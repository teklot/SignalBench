# SignalBench

**A telemetry analysis workbench suitable for real test campaigns.**

SignalBench is the cleanest binary telemetry decoding desktop tool a CubeSat engineer can install and use within 5 minutes. It is a high-performance, engineer-grade telemetry workbench designed for aerospace test and telemetry engineers that supports CSV, binary logs, and live network or serial streams. Decode, visualize, and analyze telemetry without the need for custom scripting.

## 🚀 Features

- **Multiple Format Support**: Load and decode CSV files or binary telemetry using YAML-defined packet schemas
- **Live Network Streaming**: Connect via **TCP (Client)** or **UDP (Listener)** to decode and visualize data in real-time
- **Live Serial Streaming**: Connect to COM ports with full control over Baud Rate, Parity, and Stop Bits
- **Independent Tab Architecture**: Each plot tab maintains its own independent data source (File, Serial, or Network) and configuration
- **High Performance**: Handles large files (> 500K+ records) and high-frequency streams efficiently using a Hybrid (In-Memory/SQLite) data store
- **Visualization**: Interactive plots with a **Time-Based Rolling Window** (e.g., show the last 10 seconds of live data)
- **Derived Signals**: Create custom calculated signals using math expressions (e.g., `sqrt(battery_voltage)`)
- **Data Logging**: Record raw network or serial streams directly to disk while visualizing
- **Session Management**: Save and restore workspace sessions

## 🚀 Getting Started (Quick Start)

### 1. Load a Packet Schema
Before loading binary data or streaming, the app needs to know the structure of the packets.
*   Click **"Open Schema"** in the **SCHEMA** group.
*   Select a `.yaml` schema file.

### 2. Live Streaming (Serial or Network)
Streaming settings are **per-tab**. You can stream from multiple different sources simultaneously in different tabs.

#### Serial Port
*   **Configure**: Right-click the **Serial icon** in the **STREAM** group (or use the File menu).
    *   Select your **COM Port**, **Baud Rate**, and parameters.
    *   Set the **Rolling Window** in seconds (e.g., 10s) to control the live plot width.
*   **Start**: Left-click the **Serial icon**.

#### Network (TCP/UDP)
*   **Configure**: Right-click the **Network icon** in the **STREAM** group.
    *   **Protocol**: Choose **UDP** (listens on a local port) or **TCP** (connects to a server).
    *   **IP Address**: For TCP, the server IP. For UDP, optionally bind to a specific local interface.
    *   Set the **Rolling Window** in seconds.
*   **Start**: Left-click the **Network icon**.

### 3. Static Telemetry Data
*   Click **"Open Telemetry"** in the **DATA FILE** group.
    *   **CSV**: Detects headers and loads data automatically.
    *   **Binary**: Uses the selected schema to decode the file.

### 4. Navigation & Playback
*   For file-based data, use the **Timeline Slider** and playback buttons (Play, Fast Forward/Backward, Step) to navigate.
*   A high-precision timestamp display shows the exact date and time (with milliseconds) of the current frame.

## 📋 Requirements & Setup

- **Platform**: Windows, Linux, macOS (Cross-platform via Avalonia)
- **Runtime**: .NET 9.0 SDK
- **Hardware**: Serial port access or Network (Ethernet/WiFi) connectivity
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
3.  In SignalBench, configure Serial for `COM2` and start streaming.

---

*Built for engineers who need results, not fluff.*
