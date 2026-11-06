# Image to ASCII

Выбери картинку, и сайт нарисует её символами. Ползунок меняет детализацию от низкой до максимальной. Белый фон и розовый орнамент вдохновлены оформлением PS3 Final Fantasy XIII Edition.

## Запуск

Установи .NET 9 SDK и выполни из папки проекта:

```sh
dotnet run --project web/ImageToAscii.Web.csproj --no-launch-profile -- --urls http://localhost:5080
```

Открой [localhost:5080](http://localhost:5080), нажми **«Выбрать файл»** и загрузи PNG, JPEG или BMP. Результат появится под ползунком. Можно менять уровень детализации с тем же файлом.

## Как это работает

Страница отправляет файл в ASP.NET Core. Сервер читает изображение через SkiaSharp, усиливает контуры и заменяет участки картинки символами по яркости.

`POST /api/convert` принимает `multipart/form-data` с полями `image` (файл) и `width` (ширина от 32 до 240, по умолчанию 96). Ответ приходит в формате `text/plain`. Можно загрузить файл до 8 МиБ и 16 мегапикселей. Результат ограничен 2 000 строками.

Пример запроса для файла `picture.png`:

```powershell
curl.exe -F "image=@picture.png" -F "width=120" http://localhost:5080/api/convert
```

## Тесты

```sh
dotnet test tests/ImageToAscii.Web.Tests.csproj
node --test tests/page.test.cjs
```

Первая команда проверяет конвертер и API. Вторая проверяет логику ползунка и обработку ответов сервера.

## Консольная версия

На Windows конвертер можно запустить и из терминала. Последние два аргумента задают ширину и имя выходного файла.

```sh
dotnet run --project ImageToAscii.csproj -- "picture.png" 96 "art.txt"
```
