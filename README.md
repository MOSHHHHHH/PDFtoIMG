# ממיר PDF לתמונות (PDFtoIMG)

תוכנת Windows ניידת בקובץ בודד (ללא התקנה) להמרת קבצי PDF לתמונות ברזולוציה גבוהה.

## תכונות

- המרת מספר קבצי PDF יחד, כל אחד לתיקייה נפרדת משלו
- שם תיקייה = שם הספר (ברירת מחדל: שם הקובץ, ניתן לעריכה)
- שם קובץ = "שם הספר" + מספר עמוד מרופד באפסים לפי כמות העמודים
- רזולוציה מותאמת: 150 / 300 / 600 DPI, או ערך מותאם אישית (72-1200)
- פורמט פלט: PNG (ללא אובדן איכות) או JPEG (קובץ קטן יותר)
- ממשק בעברית מלאה, עם תמיכה נכונה בטקסט מעורב עברית/לועזית (RTL native דרך WPF)
- טיפול עמיד בשגיאות - קובץ פגום אחד לא עוצר את שאר ההמרה, ונרשם ליומן שגיאות
- כתיבת קבצים אטומית (למניעת תמונות פלט פגומות במקרה של קריסה/עצירה)

## טכנולוגיה

| רכיב | בחירה | נימוק |
|---|---|---|
| שפה / ממשק | C# / .NET 8, WPF | תמיכת BiDi ילידית של Windows - הצגה תקינה של עברית/לועזית מעורבת |
| מנוע המרת PDF | [PDFtoImage](https://github.com/sungaila/PDFtoImage) (עוטף PDFium) | אותו מנוע רינדור שבכרום - אמין מול קבצים לא תקניים, מהיר |
| פרסום | `dotnet publish` self-contained + single-file | קובץ exe בודד, ללא תלות ב-.NET מותקן מראש |

## מבנה המאגר

```
src/PdfToImages/         קוד המקור (WPF)
  Services/               לוגיקת המרה ושמות קבצים
  Models/                 מודל עבודה (ConversionJob)
.github/workflows/        בנייה ופרסום אוטומטיים ל-GitHub Releases
docs/                      אתר נחיתה (GitHub Pages)
```

## בנייה מקומית

```powershell
dotnet publish src/PdfToImages/PdfToImages.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

הקובץ יופיע בנתיב `publish/PdfToImages.exe`.

## בנייה אוטומטית (CI)

Push של תגית בפורמט `vX.Y.Z` (למשל `v1.0.0`) מפעיל את ה-workflow ב-`.github/workflows/build.yml`,
אשר בונה את התוכנה על `windows-latest`, מוודא שהקובץ שנוצר תקין, ומפרסם אותו כ-GitHub Release
תחת השם `PDFtoIMG.exe`. ניתן גם להריץ את ה-workflow ידנית (`workflow_dispatch`) לבניית בדיקה בלבד.

## הערה לגבי גרסת ספריית PDFtoImage

הקוד נכתב מול ה-API הציבורי המתועד של הספרייה `PDFtoImage` (גרסה 4.x). מומלץ להריץ
`dotnet restore` ו-`dotnet build` פעם אחת מקומית (או להסתמך על ריצת ה-CI הראשונה) כדי לוודא
התאמה מלאה לגרסה העדכנית ביותר בעת הבנייה בפועל - חתימות מתודות עשויות להשתנות מעט בין גרסאות.

## רישיון

ראו קובץ [LICENSE](LICENSE).
