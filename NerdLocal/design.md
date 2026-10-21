# NerdLocal — UI Design Specification

## 1. Product Overview

**Product name:** NerdLocal  
**Subtitle:** Local Development Manager  
**Platform:** Windows desktop application  
**UI framework:** WPF (XAML)  
**Primary use case:** Manage local development domains and inspect/edit local web-server configuration files, especially for a XAMPP/Apache environment.

This document describes the current NerdLocal UI design: a compact, flat desktop interface with a light theme, red accent color, square corners, subtle borders, and no drop shadows.

## 2. Design Principles

- **Compact and practical:** Keep controls and spacing efficient so the user can manage domains without unnecessary scrolling.
- **Flat visual language:** Use square corners, thin borders, and no shadow effects.
- **Clear hierarchy:** Distinguish page sections, field labels, primary actions, and secondary actions through typography and color.
- **Consistent controls:** Text boxes, buttons, tabs, and tables share a common visual system.
- **Functional first:** Prioritize legibility and quick access to domain management and configuration tools.

## 3. Color Palette

| Token | Hex | Usage |
|---|---|---|
| Accent | `#E84444` | Primary buttons, selected tab indicator, focus border, domain count |
| Accent hover | `#CC3038` | Hover state for primary actions |
| Page background | `#F2F4F7` | Main application background |
| Surface / card | `#FFFFFF` | Header, panels, content areas, controls |
| Soft border | `#DCE1E8` | Panel edges, separators, table borders |
| Input border | `#D5DAE2` | Text box and button outlines |
| Main text | `#202938` | Main labels and headings |
| Secondary text | `#778195` | Helper text, subtitles, footer metadata |
| Table header background | `#F1F3F6` | DataGrid column header |
| Alternate table row | `#F8F9FB` | Alternating DataGrid rows |
| Selected tab background | `#FFF5F5` | Selected tab |
| Success indicator | `#28A66A` | Small footer status dot |
| Notes background | `#FFF8F0` | Quick notes and caution panels |
| Notes border | `#F0DFC9` | Notes panel border |
| Notes text | `#785D3C` | Notes body text |
| Success panel background | `#EAF6EE` | “LOCAL HOSTS” label |
| Success panel border | `#D3EBDD` | “LOCAL HOSTS” label border |
| Success panel text | `#23834F` | “LOCAL HOSTS” label text |

## 4. Typography

- **Font family:** Segoe UI
- **Default font size:** 12 px (WPF device-independent units)
- **Main application title:** 20 px, bold
- **Section headings:** 17 px, bold
- **Card headings:** 13 px, bold
- **Field labels:** 11 px, semibold, uppercase where used
- **Metadata/helper text:** 10–11 px
- **Primary button labels:** 12 px, semibold
- **Code/config editor:** Consolas, 12 px

Use a clear type hierarchy and avoid oversized marketing-style typography.

## 5. Window and Layout

### Main window

- Initial size: `1360 × 820`
- Minimum size: `1000 × 650`
- Startup position: centered on screen
- Background: `#F2F4F7`
- Content uses WPF `Grid` rows:
  1. Header — automatic height
  2. Main content — fills remaining space
  3. Footer — automatic height

### Header

- White background with a thin bottom border.
- Left side: red square brand mark containing a white “N”, followed by:
  - `NerdLocal`
  - `Local Development Manager`
- Right side: compact “CONFIGURED DOMAINS” counter with a large red numeric value and smaller “domains” suffix.
- Header padding: approximately `20,10`.

### Main content container

- White surface with a thin soft border.
- Outer margin: approximately `12,10,12,10`.
- Contains three tabs:
  - `Domain Manager`
  - `Config Files`
  - `Tools`

### Footer

- White background with a thin top border.
- Left: green status dot, `NerdLocal`, and `Local development environment`.
- Right: `Windows Desktop`.
- Compact vertical padding.

## 6. Component Styles

### Text boxes

- Height: 32
- Padding: `8,4`
- White background
- Main text: `#202938`
- Border: 1 px `#D5DAE2`
- Focus border: `#E84444`
- Square corners
- Standard bottom margin: 10

### Buttons

- Standard height: 32
- Primary button height: 34
- Standard padding: `10,0`
- Primary padding: `12,0`
- Font size: 12, semibold
- Standard background: white
- Standard border: 1 px `#D5DAE2`
- Square corners
- Hover background for standard buttons: `#F0F2F5`
- Primary action: red `#E84444`, white text
- Primary hover: `#CC3038`

### Combo box

- Height: 32
- Padding: `7,2`
- White background
- Border: 1 px `#D5DAE2`
- Square corners

### Tabs

- Transparent tab control background
- Tab text: 12 px, semibold
- Unselected text: `#667085`
- Selected text and bottom indicator: `#E84444`
- Selected background: `#FFF5F5`
- Padding: `14,8`

### Data grid

- White background
- Main text: `#354052`
- Border: `#DCE1E8`
- Horizontal grid lines
- Row height: 34
- Column header height: 32
- Column header background: `#F1F3F6`
- Alternating rows: `#F8F9FB`
- Column headers: 10 px, semibold
- Compact cell padding
- Read-only domain list with sortable/reorderable columns

### Panels and cards

- White background
- 1 px `#DCE1E8` border
- Square corners
- No drop shadows
- Typical inner padding: 10–12

## 7. Screen Specifications

### 7.1 Domain Manager

**Purpose:** Add local development domains and view existing domain configurations.

**Layout:**
- Main tab content has a two-column layout.
- Left form column: fixed width of approximately 285.
- Inter-column gap: 12.
- Right domain list: takes remaining width.

**Left panel — Add New Domain**
- Heading: `Add New Domain`
- Supporting text: `Configure a local website.`
- Field: `DOMAIN NAME`
  - Text box name: `txtDomain`
  - Tooltip example: `myproject.local`
- Field: `PROJECT DIRECTORY`
  - Text box name: `txtPath`
  - Tooltip: project web root
- Button: `Browse Project Folder`
  - Event handler: `btnBrowse_Click`
- Field: `APACHE PORT`
  - Combo box name: `cmbPort`
  - Options: `80`, `8080`, `8081`, `8082`, `3000`, `5000`, `9000`
- Primary button: `+ Add Domain`
  - Event handler: `btnAddDomain_Click`
- Notes panel titled `QUICK NOTES`:
  - Use `.local` or `.test` domains.
  - Select the correct web root.
  - Run as Administrator.
  - Backups are created before changes.
  - Restart Apache after changes.

**Right panel — Existing Domains**
- Heading: `Existing Domains`
- Supporting text: `Configured local websites`
- Status label: `LOCAL HOSTS`
- Search box: `txtSearch`
  - Tooltip: search by domain, path, or port
- Data grid: `DomainGrid`
  - `DOMAIN` column, width 180
  - `PORT` column, width 70
  - `PROJECT DIRECTORY` column, fills remaining space, minimum width 160
- Helper text: `Domain changes may require an Apache restart.`

### 7.2 Config Files

**Purpose:** Load, preview, edit, and save local configuration files.

**File path fields:**
- `Hosts File` — `txtHostsFile`
  - Default: `C:\Windows\System32\drivers\etc\hosts`
- `VHosts File` — `txtVhostFile`
  - Default: `C:\xampp\apache\conf\extra\httpd-vhosts.conf`
- `HTTPD Config` — `txtHttpdFile`
  - Default: `C:\xampp\apache\conf\httpd.conf`

**Actions:**
- `Load Hosts` — `LoadHosts_Click`
- `Load VHosts` — `LoadVHosts_Click`
- `Load HTTPD` — `LoadHttpd_Click`
- `Preview` — `PreviewConfig_Click`
- Primary action `Save File` — `SaveConfig_Click`

**Editor:**
- Control name: `txtConfigEditor`
- Multiline editable text box
- Font: Consolas, 12 px
- Dark editor background: `#171D29`
- Text color: `#E3EAF4`
- Supports tabs and new lines
- Horizontal and vertical scrollbars
- No text wrapping

### 7.3 Tools

**Purpose:** Provide shortcuts to configuration files and XAMPP utilities.

Two-column layout:

**Configuration Tools**
- Supporting text: `Open your local configuration files.`
- `Open Hosts File` — `OpenHostsFile_Click`
- `Open VHosts File` — `OpenVHostsFile_Click`
- `Open HTTPD Config` — `OpenHttpdFile_Click`

**XAMPP Tools**
- Supporting text: `Open the XAMPP control panel.`
- `Open XAMPP Control Panel` — `OpenXampp_Click` (primary red button)
- `Apache Restart Instructions` — `RestartApache_Click`
- Caution panel:
  - Title: `IMPORTANT`
  - Text: `Validate Apache configuration and check its error log if the service fails to start.`

## 8. Icon and Branding

- Brand name: `NerdLocal`
- Header mark: red square with white `N`
- Icon filename: `logo.ico`
- Recommended window XAML property: `Icon="logo.ico"`
- Recommended project property in the `.csproj`: `<ApplicationIcon>logo.ico</ApplicationIcon>`
- The `.csproj` icon setting controls the executable icon; the XAML `Icon` setting controls the WPF window icon.
- Keep the icon file included in the project and use a suitable WPF resource/build configuration for the window icon.

## 9. Interaction and Implementation Contracts

Preserve these named controls and event handlers when modifying the UI so existing code-behind remains connected.

### Named controls
- `txtTotalDomains`
- `txtDomain`
- `txtPath`
- `cmbPort`
- `DomainGrid`
- `txtSearch`
- `txtHostsFile`
- `txtVhostFile`
- `txtHttpdFile`
- `txtConfigEditor`

### Event handlers
- `btnBrowse_Click`
- `btnAddDomain_Click`
- `LoadHosts_Click`
- `LoadVHosts_Click`
- `LoadHttpd_Click`
- `SaveConfig_Click`
- `PreviewConfig_Click`
- `OpenHostsFile_Click`
- `OpenVHostsFile_Click`
- `OpenHttpdFile_Click`
- `OpenXampp_Click`
- `RestartApache_Click`

Do not rename these identifiers without updating `MainWindow.xaml.cs` accordingly.

## 10. Accessibility and Responsive Behavior

- Maintain clear text/background contrast.
- Keep field labels close to their inputs.
- Use tooltips to explain domain/path fields.
- The window minimum size is `1000 × 650`; test at the minimum size and at the default size.
- The DataGrid should keep its scrollbars available when columns exceed the available width.
- The Tools tab content uses a vertical `ScrollViewer`.
- Ensure keyboard focus remains visible through the red input focus border.
- Test the interface at common Windows display scaling settings.

## 11. Out of Scope / Not Guaranteed by This UI Specification

This document describes the visible UI and its XAML-level identifiers. It does not guarantee that:
- Apache configuration edits are valid for every environment.
- Hosts-file or Apache changes succeed without appropriate permissions.
- The app automatically runs with administrator privileges.
- Domain parsing, backups, rollback, validation, or service restart operations are implemented correctly.
- Every feature is tested or production-ready.

Validate these behaviors against `MainWindow.xaml.cs` and the target environment before release.

## 12. Design Summary

NerdLocal uses a compact, light, flat WPF design. A white header and footer frame a bordered content area; red identifies primary actions and active states; gray borders define sections without shadows or rounded cards. The three-tab structure separates domain management, configuration editing, and utility shortcuts while preserving the current control names and event-handler contracts.
