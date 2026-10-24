"""Independent DOCX pagination check after LibreOffice PDF export.
Usage: python tests/validate_docx_layout.py ARTIFACTS_DIR EXPORTED_PDF_DIR
Dependencies: python -m pip install -r tests/layout-requirements.txt
"""
from pathlib import Path
from io import BytesIO
from zipfile import ZipFile
import sys
from pypdf import PdfReader
from PIL import Image, ImageChops, ImageStat

source_dir, rendered_dir = map(Path, sys.argv[1:])
for count in (1, 3, 12, 125):
    source = PdfReader(source_dir / f"pages-{count}.pdf")
    rendered = PdfReader(rendered_dir / f"pages-{count}.pdf")
    assert len(source.pages) == len(rendered.pages) == count, f"Page count mismatch: {count}"
    with ZipFile(source_dir / f"pages-{count}.docx") as docx:
        for index, (original, page) in enumerate(zip(source.pages, rendered.pages), 1):
            expected_size = [float(original.mediabox.width), float(original.mediabox.height)]
            if original.rotation % 180:
                expected_size.reverse()
            actual_size = [float(page.mediabox.width), float(page.mediabox.height)]
            assert all(abs(a - b) < .1 for a, b in zip(expected_size, actual_size)), (count, index, actual_size)
            # Writer shares the resource dictionary across pages; count actual drawing
            # operators rather than every image available in that dictionary.
            drawings = [args[0] for args, op in page.get_contents().operations if op == b"Do"]
            assert len(drawings) == 1, f"Blank or unexpected drawing count: {count}, page {index}"
            expected = Image.open(BytesIO(docx.read(f"word/media/page{index}.png"))).convert("RGB")
            actual = page.images[drawings[0]].image.convert("RGB").resize(expected.size)
            # Exporters can recompress PNG as JPEG. Verify the same visible page, in order.
            error = ImageStat.Stat(ImageChops.difference(expected, actual)).mean
            assert max(error) < 5, (count, index, "Image appearance changed", error)
    print(f"PASS: {count} pages; dimensions, orientation, image order and appearance; no blank pages")
