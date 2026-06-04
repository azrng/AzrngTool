## 1. Dependency and Configuration

- [x] 1.1 ~~Copy `C:\Downloads\Aspose.Pdf.dll` into a project-local dependency directory~~ → Replaced with Aspose.PDF.Drawing NuGet package.
- [x] 1.2 ~~Reference the project-local `Aspose.Pdf.dll`~~ → Replaced with `<PackageReference Include="Aspose.PDF.Drawing" />` in csproj.
- [x] 1.3 Configure build output behavior so Aspose PDF is available to Debug, Release, and publish output.
- [x] 1.4 Record the Aspose.PDF.Drawing NuGet package version in implementation notes or devlog.
- [x] 1.5 Define the Aspose license loading convention, including default license file name and lookup path.
- [x] 1.6 Switch csproj from local DLL reference to `PdfPig` + `PdfSharp` + `DocumentFormat.OpenXml` NuGet packages and remove `Libs/Aspose/Aspose.Pdf.dll`.
- [x] 1.7 Fix error logging: use `LocalLogHelper.LogError` + `ex.GetExceptionAndStack()` in all PDF service catch blocks.
- [x] 1.8 Fix `BuildFriendlyError` to handle meaningless exceptions.
- [x] 1.9 Remove evaluation mode UI hints from PDF management page.

## 2. PDF Domain and Service Layer

- [x] 2.1 Add PDF management models for loaded file summary, page selection result, split request, split result, Word export request, and processing result.
- [x] 2.2 Implement a page range parser that supports single pages and closed ranges such as `1,3-5,8`.
- [x] 2.3 Add `IPdfProcessingService` with methods for loading PDF metadata, splitting selected pages, splitting pages individually, and exporting Word.
- [x] 2.4 Implement `AsposePdfProcessingService` using Aspose.PDF `Document`, page copy APIs, and DOCX save options.
- [x] 2.5 Convert Aspose and file-system exceptions into stable Chinese error messages.
- [x] 2.6 Ensure PDF processing operations run asynchronously from the ViewModel command boundary.

## 3. ViewModel and Navigation

- [x] 3.1 Add `PdfManagementPageViewModel` with selected file state, page range input, split mode, output path state, busy state, and result feedback.
- [x] 3.2 Add commands for choosing PDF file, choosing split output target, choosing Word output path, splitting PDF, exporting Word, and clearing current file.
- [x] 3.3 Disable file selection and export commands while PDF processing is running.
- [x] 3.4 Register the PDF management ViewModel and service in the existing dependency injection setup.
- [x] 3.5 Add the PDF management entry to the main navigation and update tool counts or homepage copy if needed.

## 4. User Interface

- [x] 4.1 Add `PdfManagementPage` following existing Avalonia page layout and design tokens.
- [x] 4.2 Implement empty, loaded, processing, success, and error states.
- [x] 4.3 Provide controls for file selection, PDF summary, page range input, split mode selection, output target selection, split action, and Word export action.
- [x] 4.4 Show evaluation mode and conversion limitation hints when Aspose license is not configured.
- [x] 4.5 Verify the page does not overflow or overlap at common desktop window sizes.

## 5. Tests

- [x] 5.1 Add unit tests for valid page range parsing, including mixed single pages and ranges.
- [x] 5.2 Add unit tests for empty, malformed, reversed, duplicate, and out-of-range page expressions.
- [x] 5.3 Add ViewModel tests for command availability before and after PDF file load.
- [x] 5.4 Add ViewModel tests for busy state and duplicate operation prevention.
- [x] 5.5 Add service-level tests or integration smoke checks for Aspose PDF metadata loading, PDF split, and Word export.
- [x] 5.6 Add tests or documented equivalent verification for missing Aspose license evaluation mode messaging.

## 6. Verification and Delivery

- [x] 6.1 Run the existing test suite or the affected PDF/ViewModel test subset.
- [x] 6.2 Run Debug build and fix compile or XAML binding issues.
- [x] 6.3 Run Release build or publish-equivalent verification to confirm PdfPig + PDFsharp + OpenXML packaging compatibility.
- [ ] 6.4 Perform an application-level smoke test for loading a PDF, splitting pages, and exporting Word.
- [x] 6.5 Update `TASK.md` and add a `doc/devlog/` record when implementation is complete.
- [x] 6.6 Verify Word export works end-to-end with PdfPig + OpenXML SDK after csproj migration.
