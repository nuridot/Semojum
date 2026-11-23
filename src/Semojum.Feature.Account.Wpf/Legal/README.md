# Login legal documents

Source: Notion Service / Service DB, `2차 변경안 (11.09)` only.
- Terms: https://app.notion.com/p/3c543813bc2e80df86b1d833a210299f
- Privacy: https://app.notion.com/p/3c543813bc2e80f396eed44306e35289
- Retrieved: 2026-11-10. The user explicitly requested preserving pending decisions and verification notes verbatim.

The Markdown snapshots preserve the source wording; the XAML resources are generated selectable text, not screenshots. Update the snapshots and run `python scripts/build-legal-documents.py` to regenerate them. Do not silently resolve placeholders or mix in the archived August drafts.

The legal windows use the final APP_UI frames [Terms](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11821) and [Privacy](https://www.figma.com/design/HJfwT8zwFbQZAIUR3Bk6Cl/?node-id=58833-11842). The first two paragraphs supply the window title and subtitle. The reader displays the remaining clauses, lists, tables and links. Both frames use the 16×16 close icon in `Semojum.UI.Common/Assets/Figma/form-close.svg`.
