# PDFtoIMG — ממיר PDF לתמונות ול־Word

תוכנת Windows ניידת בקובץ EXE יחיד, עם ממשק עברי מלא. המרת קובצי PDF לתמונות PNG/JPEG או למסמכי Word ‏(DOCX), ללא אינטרנט, ללא התקנת Microsoft Word וללא התקנת .NET בנפרד.

## סוגי הפלט

### תמונות PNG/JPEG

- כל PDF נשמר בתיקייה נפרדת בשם הספר; אפשר לערוך את השם בטבלה.
- כל עמוד נשמר כתמונה ממוספרת: למשל `שם הספר 001.png`.
- PNG ללא אובדן איכות או JPEG לחיסכון במקום.
- ברירת המחדל היא 300 DPI. זמינים 150, 200, 300, 600 או ערך מותאם אישית בטווח 72–1200.
- הרצה חוזרת מחליפה תמונות בעלות אותו שם, בהתנהגות הקיימת.

### מסמך Word ‏(PDFtoDOCX) — חדש ב־1.0.0-beta.1

- כל PDF מומר למסמך DOCX נפרד בתיקיית היעד: חמישה קובצי PDF ייצרו חמישה מסמכים.
- כל עמוד מרונדר כתמונת PNG ומוטמע בעמוד Word נפרד, לפי סדר המקור.
- **אין OCR, חילוץ טקסט או המרה לתוכן הניתן לעריכה.** התוכן נשאר תמונה, לרבות עברית, נוסחאות ועיצוב.
- מידות וכיווני העמודים נשמרים לכל עמוד בנפרד, כולל שילוב לאורך, לרוחב ועמודים מסובבים.
- ברירת המחדל היא **200 DPI**. אפשר לבחור גם 300/600 DPI או ערך מותאם. DPI גבוה מגדיל את זמן ההמרה ואת גודל המסמך.
- כל התמונות נמצאות בתוך ה־DOCX; אין קישורים לקבצים חיצוניים ואין קובצי תמונה זמניים בדיסק.
- שם קיים לא יידרס: `ספר.docx`, אחריו `ספר (2).docx`, וכן הלאה. גם שמות כפולים ברשימה מקבלים סיומת ואפשר לערוך אותם.
- המסמך נכתב לקובץ זמני באותה תיקיית יעד ומקבל שם סופי רק לאחר סיום וסגירה מוצלחים. כישלון או ביטול מוחקים את הפלט הזמני; מסמך חלקי לא יפורסם.

## שימוש

1. הוסיפו קובצי PDF באמצעות הכפתור או בגרירה לחלון.
2. בחרו סוג פלט: **תמונות PNG/JPEG** או **מסמך Word (DOCX)**.
3. בחרו DPI, ובמצב תמונות גם PNG או JPEG.
4. בחרו תיקיית יעד ושמות ייחודיים ותקינים, ולחצו **התחל המרה**.

ההתקדמות מחושבת לפי עמודים שטופלו. בזמן ההמרה ההגדרות ועריכת הרשימה נעולות. כישלון בקובץ אחד אינו עוצר את שאר הקבצים, ופרטים נוספים מצורפים ל־`log-שגיאות.txt` בתיקיית היעד.

## מגבלות

- Windows 10/11, ‏64 סיביות. ממשק WPF אינו פועל ב־Linux.
- PDF פגום או מוגן בסיסמה מדווח כשגיאה; אין בשלב זה הזנת סיסמה בממשק.
- Word מוגבל לממד נייר מרבי של 22 אינץ׳: עמודים גדולים יותר מוקטנים באופן יחסי. עמודים צרים במיוחד מקבלים משטח מינימלי של 0.1 אינץ׳, ללא עיוות התמונה.
- במצב DOCX, עמוד הדורש יותר מ־80 מיליון פיקסלים ברזולוציה שנבחרה נדחה עם המלצה להפחית DPI, כדי לצמצם סכנת מחסור בזיכרון.
- התמונות מעובדות אחת בכל פעם ונכתבות ישירות לאריזת DOCX במצב כתיבה רציפה. XML זמני קטן נכתב בנפרד ונמחק בסגירתו. מסמכים ארוכים עדיין זקוקים לשטח דיסק עבור המסמך כולו ולזיכרון עבור עמוד בודד, מנוע PDFium ומטא־נתונים.
- המיקום במסמך תוכנן ונבדק ללא עמודי הפרדה ריקים. הפריסה המדויקת ב־Microsoft Word דורשת גם בדיקה ידנית ב־Windows; בדיקת LibreOffice אינה תחליף מלא לכך.

## טכנולוגיה ומבנה

- C# / .NET 8 / WPF: ממשק RTL עברי.
- `PDFtoImage` ‏4.1.1 ו־PDFium: רינדור PDF; ‏SkiaSharp: קידוד התמונות.
- `DocumentFormat.OpenXml` ‏3.3.0: יצירת תוכן Word ובדיקת תקינותו; `System.IO.Packaging`: כתיבת האריזה ברצף, בלי לצבור את כל התמונות בזיכרון.

```text
src/PdfToImages/Services/PdfConverterService.cs   המרה לתמונות
src/PdfToImages/Services/DocxConverterService.cs  המרה למסמך Word
src/PdfToImages/Services/FileNamingService.cs     שמות קבצים
src/PdfToImages/MainWindow.xaml[.cs]              ממשק ולוגיקת הפעלה
src/PdfToImages/Models/ConversionJob.cs           מודל משימה
tests/PdfToImages.Tests/                        בדיקות המרה אמיתיות
tests/validate_docx_layout.py                   בדיקת פריסה עצמאית
.github/workflows/                              בדיקות, בנייה ופרסום
docs/                                          אתר GitHub Pages
```

## פיתוח ובדיקות

נדרש SDK של .NET 8. בדיקת קישור ההורדה באתר דורשת Node.js 18 ומעלה. ספריות הרינדור מסופקות דרך NuGet. הבדיקות מקשרות את קוד השירותים המקורי לפרויקט שאינו תלוי ב־WPF, וניתנות להרצה ב־Windows וב־Linux:

```sh
node --test tests/release-fetcher.test.cjs
dotnet restore tests/PdfToImages.Tests/PdfToImages.Tests.csproj
dotnet test tests/PdfToImages.Tests/PdfToImages.Tests.csproj -c Release --no-restore
dotnet restore src/PdfToImages.sln -p:EnableWindowsTargeting=true
dotnet build src/PdfToImages.sln -c Release --no-restore -p:EnableWindowsTargeting=true
```

הבדיקות יוצרות PDF אמיתי של עמוד אחד, 3, 12 ו־125 עמודים, כולל כיוונים מעורבים וסיבוב. הן מאמתות את סכמת Open XML, מספר התמונות והמקטעים, המידות, סדר העמודים, פיקסלים מול רינדור המקור, היעדר קישורים חיצוניים, שמות כפולים, יעד קיים, כישלון באמצע, ביטול וקבצים חסרים/פגומים/מוצפנים. נבדקות גם המרות PNG ו־JPEG והרצה חוזרת שלהן.

לבדיקת פריסה עצמאית באמצעות LibreOffice, שמרו את תוצרי הבדיקות באמצעות המשתנה `PDFTOIMG_TEST_ARTIFACTS` ואז הריצו (Bash; נדרשים LibreOffice ו־Python):

```sh
export PDFTOIMG_TEST_ARTIFACTS=/tmp/pdftoimg-fixtures
dotnet test tests/PdfToImages.Tests/PdfToImages.Tests.csproj -c Release
python -m pip install -r tests/layout-requirements.txt
mkdir -p /tmp/pdftoimg-layout
libreoffice --headless --convert-to pdf --outdir /tmp/pdftoimg-layout "$PDFTOIMG_TEST_ARTIFACTS"/*.docx
python tests/validate_docx_layout.py "$PDFTOIMG_TEST_ARTIFACTS" /tmp/pdftoimg-layout
```

בדיקה זו מאמתת את מספר העמודים בפועל, מידות, כיוון, סדר ומראה התמונות, והיעדר עמודים ריקים. היא מופעלת גם ב־CI על Linux; בדיקות ההמרה ובניית WPF מופעלות גם על Windows.

## בנייה והפצה כ־EXE יחיד

```sh
dotnet publish src/PdfToImages/PdfToImages.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -o publish
```

הפלט הוא `publish/PdfToImages.exe`. המתג `EnableWindowsTargeting` מאפשר בנייה ב־Linux; הוא אינו מאפשר להריץ בו את ממשק Windows.

Push של תגית גרסה כגון `v1.0.0-beta.1` מפעיל את `.github/workflows/build.yml`: בדיקות המרה על Windows, בניית EXE עצמאי, אימות חתימת PE וגודל, והעלאת `PDFtoIMG.exe` ל־GitHub Release. גרסאות עם מקף מסומנות כ־Pre-release. מספר גרסת הקובץ נשמר מספרי גם כשגרסת ההפצה כוללת סיומת beta. אפשר להפעיל ידנית באמצעות `workflow_dispatch` לבניית בדיקה בלבד. יש לבדוק גם את הפעלת הממשק והמרה ידנית ב־Windows לפני הפצה יציבה.

## רישיון

[MIT](LICENSE).
