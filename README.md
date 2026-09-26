# Open — Local Face Photo Matching

Open is a Windows desktop application for maintaining a small, local person registry and comparing a submitted photograph against reference photographs stored in that registry. It provides a WPF interface for selecting a photo, showing a scan animation while the comparison runs, reviewing a proposed record, and explicitly confirming or rejecting that proposal.

> **Prototype notice:** This project is an experimental, local-first prototype. Its face comparison is based on OpenCV's LBPH recognizer. It is not a production biometric identity system, a security boundary, or a substitute for human verification.

## Contents

- [Features](#features)
- [Screens and workflow](#screens-and-workflow)
- [Technology](#technology)
- [Requirements](#requirements)
- [Build from source](#build-from-source)
- [Run the application](#run-the-application)
- [Use the application](#use-the-application)
- [How matching works](#how-matching-works)
- [Data storage and privacy](#data-storage-and-privacy)
- [Project structure](#project-structure)
- [Known limitations](#known-limitations)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Features

- Add, edit, search, and delete person records.
- Store each person's name, phone number, email address, notes, and reference photos.
- Keep records and photo samples in a local SQLite database.
- Select a query photo with the file picker or drag and drop it into the photo panel.
- Preview the selected photo before starting a comparison.
- Show an animated scan line while the local comparison is running.
- Display the nearest candidate and its contact information for review.
- Require a separate confirmation action before opening the candidate's record.
- Reject an incorrect candidate and try another photo.
- Run as a self-contained Windows x64 deployment so a separate .NET runtime installation is not required on the target PC.

## Screens and workflow

The main window is organized into two side-by-side panels:

1. **Photo input:** choose a photo or drag one into the preview. The application checks whether OpenCV can decode the file before enabling the comparison action.
2. **Scan and result:** start the comparison, watch the animated scan indicator, review the proposed person, then confirm or reject the candidate.

The **Manage people** window is opened from the top-right button. It contains the person list and the add/edit form. New records require at least two reference photos. Existing records can be updated with additional photos.

The animation is a WPF presentation effect. The actual comparison is local image processing performed by OpenCV; the animation is not a separate sensor or forensic scan.

## Technology

| Area | Implementation |
| --- | --- |
| Language | C# |
| UI | Windows Presentation Foundation (WPF) |
| Target framework | `.NET 9` for Windows (`net9.0-windows`) |
| Computer vision | OpenCvSharp4 `4.10.0.20241108` |
| Recognition algorithm | OpenCV LBPH face recognizer |
| Database | SQLite through Microsoft.Data.Sqlite `9.0.2` |
| Deployment target | Windows x64, self-contained publish |

## Requirements

### To build from source

- Windows 10 or later (Windows 11 recommended).
- .NET 9 SDK with the Windows Desktop workload/runtime components available.
- NuGet access for restoring the pinned package dependencies.
- A Windows x64 environment for the documented `win-x64` publish command.

### To run the published app

- Windows x64.
- No separate .NET runtime installation is required for the self-contained package.
- The published directory must remain intact. Run the executable from inside that directory; it depends on the adjacent managed and native runtime files.

## Build from source

Open PowerShell or Command Prompt in the repository root:

```powershell
dotnet restore OpenFaceRegistry.sln --runtime win-x64
dotnet build OpenFaceRegistry.sln --configuration Release --no-restore
```

To create a self-contained Windows x64 deployment:

```powershell
dotnet publish OpenFaceRegistry.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output publish-win-x64
```

The publish output is written to `publish-win-x64/`. Keep all files in that directory together. The `.exe` is a small app host; the rest of the self-contained runtime and native OpenCV/SQLite libraries are also required.

You can also open `OpenFaceRegistry.sln` in Visual Studio and select the `Release` configuration.

## Run the application

After publishing, run:

```powershell
.\publish-win-x64\OpenFaceRegistry.exe
```

Or double-click `publish-win-x64\OpenFaceRegistry.exe` in File Explorer.

For a development run after package restore:

```powershell
dotnet run --project OpenFaceRegistry.csproj
```

## Use the application

### Add a person

1. Open **Manage people**.
2. Select **New**.
3. Enter a name. Phone, email, and notes are optional.
4. Choose at least two reference photos when saving a new person.
5. Confirm that the record appears in the list.

Additional reference photos can be added by selecting an existing person and saving the record with more photos selected.

### Search by photo

1. Choose an image with **Choose photo**, or drag an image file into the left preview area.
2. Review the preview and select **Find match**.
3. Wait for the local comparison to finish.
4. Review the proposed record and its distance value.
5. Select **Confirm match and open record** to view the record, or **This is not the match** to reject it.

The picker shows all files so it can report an understandable decode error for unsupported or non-image files. A file being selectable does not mean every file type can be decoded; OpenCV must support the actual image encoding.

## How matching works

`FaceMatcher` currently performs the following steps:

1. Decode each stored sample and the query photo with OpenCV in grayscale.
2. Resize the **entire image** to `160 × 160` pixels.
3. Apply histogram equalization.
4. Train an LBPH recognizer with the stored samples and their person IDs.
5. Predict the nearest label for the query and return OpenCV's distance value.
6. The UI treats distances at or below `75` as a candidate. This is a prototype threshold, not a probability or a calibrated confidence score.

LBPH distances are relative to the images and settings in the current registry. A lower distance means a closer algorithmic match; it does not mean a percentage chance that the identity is correct.

## Data storage and privacy

The database is created at:

```text
%LOCALAPPDATA%\OpenFaceRegistry\registry.db
```

Person metadata and the original bytes of each reference photograph are stored in SQLite. The application does not currently upload photographs or records to a server. The database is **not encrypted by this prototype**. Anyone with access to the Windows account and database file may be able to read its contents.

Only add photographs and personal information that you are authorized to use. Limit access to the Windows account, protect backups, and delete records when they are no longer needed. Before using real biometric or personal data, assess applicable privacy, consent, retention, and security requirements for your jurisdiction and use case.

## Project structure

```text
OpenFaceRegistry.sln        Visual Studio solution
OpenFaceRegistry.csproj     WPF project and pinned NuGet dependencies
App.xaml                    WPF application resources and startup definition
App.xaml.cs                 Application entry point
AssemblyInfo.cs             Assembly metadata
MainWindow.xaml             Photo input, scan animation, and candidate review UI
MainWindow.xaml.cs          Photo loading, scan state, and confirmation workflow
PeopleWindow.xaml           Person registry and editing UI
PeopleWindow.xaml.cs        Person CRUD and reference-photo selection
PersonRecord.cs             Person and face-sample data models
RegistryDatabase.cs         SQLite schema and data access
FaceMatcher.cs              OpenCV preprocessing and LBPH matching
README.md                   Project documentation
```

## Known limitations

- **No automatic face detection or alignment:** the matcher resizes the entire image. It does not find a face region, detect multiple faces, align eyes, or reject an image that contains no face. For better results, prepare tightly cropped, front-facing images with one clearly visible face before adding them or searching.
- **LBPH is sensitive to image conditions:** lighting, pose, expression, camera quality, cropping, and background differences can change results. Multiple varied reference photos may help but do not guarantee a correct match.
- **One nearest candidate:** the current matcher returns one nearest label rather than a ranked list of candidates.
- **Threshold is uncalibrated:** `75` is a configurable starting value in `MainWindow.xaml.cs`. It must be evaluated on representative data before relying on it.
- **No account system or audit log:** anyone who can launch the app under the same Windows account can access the registry.
- **No database encryption:** protect the device and database file using operating-system controls and full-disk encryption.
- **Local development only:** the app has not been designed for multi-user access, remote deployment, high availability, or large-scale search.
- **Human review is required:** a proposed match can be wrong or missing. Do not use the result as the sole basis for a consequential decision or as a standalone authentication factor.
- **No automated test suite is included yet.**

## Troubleshooting

### NuGet restore fails

Check that the machine can reach the configured NuGet feeds, then retry:

```powershell
dotnet restore OpenFaceRegistry.sln --runtime win-x64
```

If your environment uses an approved internal NuGet mirror, configure that feed before restoring. Do not commit private feed credentials or API keys.

### The executable is missing native libraries

Do not copy only `OpenFaceRegistry.exe`. Re-run `dotnet publish` and distribute the complete `publish-win-x64` directory, including the `runtimes` folder.

### An image cannot be opened

The file picker accepts all file names so users can choose a photo from any folder. OpenCV must still decode its format. Convert unsupported files to JPEG or PNG and try again. Very large or corrupt images may also fail.

### The result seems wrong

Use a single-person, tightly cropped, front-facing image with lighting similar to the reference photos. Review the candidate manually. To change the acceptance threshold, edit `MatchDistanceLimit` in `MainWindow.xaml.cs` and evaluate the change with representative positive and negative examples.

### Reset local application data

Close the application and back up or remove `%LOCALAPPDATA%\OpenFaceRegistry\registry.db`. Removing this file permanently removes all local records and stored photos.

## Contributing

1. Create a branch for your change.
2. Keep changes focused and explain behavior changes in the pull request.
3. Build the solution in Release mode before submitting:

   ```powershell
   dotnet build OpenFaceRegistry.sln --configuration Release
   ```

4. Do not commit generated `bin/`, `obj/`, publish output, local databases, photographs, credentials, or private user data.
5. If changing recognition or thresholds, document how false matches and missed matches were evaluated.

## License

No project license file is included. Add a `LICENSE` file with the license you choose before accepting outside contributions or publishing the project for reuse. The NuGet dependencies have their own licenses; review their package metadata separately.
