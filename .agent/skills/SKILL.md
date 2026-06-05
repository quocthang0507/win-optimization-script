# C# & WinUI 3 Performance and Compatibility Guidelines

This document outlines the standard coding practices and optimization guidelines for the Windows System Maintenance Tool codebase to ensure optimal performance, trimming/Native AOT compatibility, and clean UI rendering.

## 1. UI Performance & Return Types
When building WinUI 3 controls programmatically via helper methods:
- **Rule**: Avoid returning generic layout classes like `FrameworkElement`.
- **Practice**: Always declare the return type of visual row/component builders using their concrete type (e.g., `Border`, `Grid`, `StackPanel`, `Button`).
- **Rationale**: Returning concrete types reduces casting and boxing overhead when controls are added to collection properties (such as `Panel.Children`), satisfying IDE performance analyzers.

```csharp
// ❌ Avoid returning FrameworkElement
private FrameworkElement CreateRow() => new Grid();

//  Preferred concrete return type
private Grid CreateRow() => new Grid();
```

## 2. Collection Expression Simplification (C# 12)
- **Rule**: Use C# 12 collection expressions (`[...]`) instead of explicit instantiation (`new List<T> { ... }` or `new T[] { ... }`).
- **Practice**:
  ```csharp
  // ❌ Avoid
  var list = new List<string> { "item" };
  await ProcessItemsAsync(new List<Item> { singleItem });

  //  Preferred
  List<string> list = ["item"];
  await ProcessItemsAsync([singleItem]);
  ```

## 3. WinRT AOT & Trimming Compatibility
- **Rule**: Ensure generic collections marshaled across the WinRT ABI have native support.
- **Practice**: Enable `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the `.csproj` file to allow WinRT marshallers to run unsafe AOT-optimized interop code.
- **Background**: WinUI 3 generic collection boundaries require unsafe marshaling code generation to function correctly under Native AOT or trimmed deployments.

## 4. UI Elements and Icon Styling
- **Button Padding**: Standard WinUI button styles apply default padding. For small, fixed-size buttons (e.g., `42x36px` action or run buttons), reset `Padding = 0` to prevent inner symbols/icons (like `Symbol.Play` or `Symbol.Find`) from being clipped.
- **Risk Badges**: Keep `RiskBadge` border margins and widths consistent (e.g., fixed width of `96px`, center-aligned content) to maintain clean alignment regardless of container layouts.
- **Tooltips & Localization**: 
  - Every icon-only button (`IconButton`) must define a tooltip using `ToolTipService.SetToolTip(control, tooltipText)`.
  - Tooltips must be localized dynamically using translation helpers (`T(...)` or `F(...)`) instead of hardcoded strings to ensure complete support for bilingual users.

---

## 5. Modern WinUI 3 Architecture & Design Patterns

To maintain a clean, maintainable, and highly responsive codebase, developers must adhere to the following architectural and design practices:

### A. Lazy-Loading & View Caching (Navigation Architecture)
- **Concept**: Startup latency and memory footprint should be minimized. Do not load pages until they are requested.
- **Practice**:
  - Implement a caching shell mechanism (e.g., `Dictionary<string, BasePage>`) inside `MainWindow` to store page instances.
  - Instantiate view controls lazily on first navigation.
  - Retain page instances in the cache to preserve visual states (e.g. disk analyzer logs, checkbox selections) during tab switches.
  - Clear/invalidate the cache on global configuration updates (like changing the UI language or theme) to force a clean re-render.

### B. Service Decoupling (Separation of Concerns)
- **Concept**: Keep UI views thin. Views should only handle control layout, event wiring, and basic input validation.
- **Practice**:
  - Abstract all business logic, Win32 interop, registry queries, and OS tasks into reusable, stateless services (e.g., `DiskAnalysisService`, `StartupService`, `SystemStatusService`).
  - Access service singletons through a centralized coordinator or dependency manager (such as `MainWindow` accessors).

### C. Performance-First OS Queries (Avoid WMI)
- **Concept**: WMI queries (`ManagementObjectSearcher`) are notoriously slow, synchronous, and UI-blocking on Windows.
- **Practice**:
  - **Do NOT use WMI** for retrieving hardware specifications or real-time diagnostics.
  - Query hardware details directly from the Registry (e.g., query CPU Brand name via `HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0`).
  - Use native Win32 P/Invoke structures (e.g., `GlobalMemoryStatusEx`) for high-performance memory analytics.

### D. Responsive Asynchronous Task Execution & Cancellation
- **Concept**: Heavy tasks (file-system searches, system cleanups, package updates) must run in the background.
- **Practice**:
  - Execute expensive CPU-bound computations off the main thread using `Task.Run()` with async/await.
  - Always wire a `CancellationToken` to long-running tasks. Expose a physical **Stop/Cancel** button in the UI.
  - Keep the app status bar/text updated in real-time, coloring it dynamically to indicate the worker state (e.g., Green for Idle/Ready, Blue for Scanning/Running, Orange for Cancelling).

### E. Fluent Design System & Visual Polish
- **Concept**: The app must feel premium, modern, and aligned with Windows 11 Fluent Design principles.
- **Practice**:
  - Prefer Segoe MDL2 Assets glyph icons (e.g., `Symbol.Find`, `Symbol.Play`) over static image files to ensure native rendering and scalability.
  - Utilize modern styling layers like **Mica** or **Acrylic** materials for window backdrops to create a premium glassmorphic effect.
  - Leverage proportional layouts, layout grid definitions (`RowDefinitions`, `ColumnDefinitions`), and Spacing/Padding parameters instead of hardcoded margins to ensure native responsiveness on variable screen resolutions and aspect ratios.
  - Maintain a clean contrast ratio: Use semi-bold header blocks and subtle body opacity (`0.7` to `0.86`) to establish clear information hierarchy.

