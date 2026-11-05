# עמדת טעינה HopOn — Windows

אפליקציית Windows קלה במצב עמדת שירות עצמי. מסך הפתיחה בעברית, וממנו נפתח עמוד הטעינה/קריאת הכרטיס של HopOn בתוך WebView2.

## הגבלת ניווט

התוכנה מאפשרת ניווט ראשי רק אל:
- `https://hopon.co.il/read`

כל נתיב אחר באתר נחסם, ובכלל זה:
- `/charging-stations-map`
- `/about`
- `/ravkav`
- `/freeMonthly`
- `/freeMonthlyOtef`

הבדיקה נעשית מול שם המארח המדויק `hopon.co.il` והנתיב המדויק `/read`; תתי-דומיינים ואתרים אחרים אינם מורשים. גם שינויי היסטוריית ניווט פנימיים נבדקים. חלונות חדשים, הורדות וקישורים חיצוניים חסומים.

## הורדת EXE

לאחר ש-GitHub Actions יסיים בנייה בהצלחה, קובץ EXE יפורסם ב:
https://github.com/g0534160689-create/MyFirstApp/releases/latest

תהליך הבנייה:
https://github.com/g0534160689-create/MyFirstApp/actions

היישום נבנה כ-Windows x64 self-contained. נדרש WebView2 Runtime במחשב היעד.

## מגבלות חשובות

- לא ניתן להבטיח שהאתר לא יציג רכיבים פנימיים או ישנה את אופן הניווט שלו; המעטפת חוסמת ניווטי עמוד שאינם אל `/read`.
- משאבי עמוד (JavaScript, CSS, תמונות וקריאות API) נטענים לפי הצורך כדי שעמוד הקריאה והטעינה יפעל. אלה אינם ניווטי עמוד, וחסימתם עלולה לשבור את השירות.
- לא אומתה עדיין תאימות קורא הכרטיסים והתשלום ל-WebView2. כל פרוטוקול חיצוני חסום.
- יש לבדוק בפועל עם קורא USB וכרטיס לפני שימוש בעמדה אמיתית.
- אין איסוף או שמירה של פרטי אשראי בתוכנה.

## פיתוח מקומי

נדרשים Windows 10/11, .NET 10 SDK ו-WebView2 Runtime.

```powershell
dotnet restore .\RavKavKiosk\RavKavKiosk.csproj
dotnet build .\RavKavKiosk\RavKavKiosk.csproj -c Release
dotnet run --project .\RavKavKiosk\RavKavKiosk.csproj
```
