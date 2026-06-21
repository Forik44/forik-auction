# Деплой «Аукциона аниме» на бесплатный хостинг

> ⚠️ ОБНОВЛЕНИЕ СХЕМЫ БД (важно при апдейте с рероллом квестов)
> В обновлениях иногда меняется схема БД (новые поля/таблицы: реролл квестов, одобрение квестов). Приложение создаёт
> базу через `EnsureCreated()`, который НЕ добавляет новые колонки в уже существующую базу.
> Поэтому после деплоя один раз удали файл базы — он пересоздастся с новой схемой:
> File manager → `wwwroot/App_Data` → удалить `forik.db` (и `forik.db-shm`, `forik.db-wal`, если есть)
> → перезапусти приложение (Process → recycle). Тестовые комнаты/очки при этом обнулятся — на раннем этапе это нормально.

Цель: выложить приложение в интернет бесплатно так, чтобы друг заходил по ссылке,
данные сохранялись, а код можно было продолжать дорабатывать (правка → пуш в
GitHub → автодеплой).

---

## Какой хостинг выбрать

У приложения три особенности: **Blazor Server держит постоянное WebSocket-соединение**
(SignalR), нужна **постоянная база** (SQLite-файл или внешняя БД) и хочется
**автодеплой из Git**. Под это сравнение бесплатных вариантов (на 2026):

| Хостинг | Бесплатно | WebSocket | Постоянные данные | Автодеплой | Вывод |
|---|---|---|---|---|---|
| **MonsterASP.NET** | да, без карты | да (IIS) | да (файлы + бесплатная MSSQL) | GitHub Actions / FTP / WebDeploy | **рекомендую** |
| Render (free) | да | да (будит сервис) | **нет** — диск стирается; free Postgres живёт 30 дней | да (Git/Docker) | только если перейти на внешнюю БД |
| Fly.io | нет (с 2024 платно, ~$2–5/мес) | да | да (том $0.15/ГБ) | Docker | дёшево, но не бесплатно |
| Azure App Service Free F1 | да | **WebSocket выключен на Free** | — | да | не подходит для Blazor Server |

**Дальше — основной путь на MonsterASP.NET. В конце — короткая инструкция для Render
(через Docker) как запасной вариант.**

---

## Вариант 1. MonsterASP.NET (рекомендуется)

### Шаг 0. Положить код на GitHub
В папке проекта (где лежит `ForikAuction.sln`):
```bash
git init
git add .
git commit -m "Аукцион аниме: первый коммит"
```
Создайте пустой репозиторий на github.com (например `forik-auction`) и:
```bash
git branch -M main
git remote add origin https://github.com/ВАШ_ЛОГИН/forik-auction.git
git push -u origin main
```
> `.gitignore` уже настроен: `bin/`, `obj/`, `*.db` и локальные секреты в репозиторий не попадут.

> ВАЖНО: НЕ используйте встроенный в панель «Github Deploy» — он делает `git clone`
> исходников в `wwwroot` и НЕ собирает .NET (сайт не запустится). Нужен путь через
> GitHub Actions ниже: GitHub сам делает `dotnet publish` и заливает готовое
> приложение по WebDeploy. Если «Github Deploy» включён — отключите его (Disabled).

### Шаг 1. Завести бесплатный сайт на MonsterASP.NET
1. Зарегистрируйтесь на https://www.monsterasp.net/ (Free hosting, карта не нужна).
2. В панели создайте **Website** — выдадут адрес вида `https://имя.runasp.net`.
3. Откройте сайт → раздел **WebDeploy / Publish profile**. Там будут значения:
   - *Server / Computer name* (вида `site123.siteasp.net:8172` или подобное),
   - *Website name* (имя сайта в IIS),
   - *Username* и *Password*.
   Они понадобятся для GitHub Actions.

### Шаг 2. Добавить секреты в GitHub
В репозитории: **Settings → Secrets and variables → Actions → New repository secret**.
Создайте 4 секрета (значения — из шага 1):
- `WEBSITE_NAME` = `site75556` (имя сайта)
- `SERVER_COMPUTER_NAME` = `https://site75556.siteasp.net:8172`
- `SERVER_USERNAME` = `site75556`
- `SERVER_PASSWORD` = пароль из блока WebDeploy Access (кнопка Show)

Workflow уже лежит в репозитории: `.github/workflows/deploy.yml`. Он на каждый пуш в
`main` делает: setup .NET 8 → restore → прогон тестов логики → publish → WebDeploy.

> Точные имена полей в их action могут отличаться — сверьтесь с официальной
> инструкцией: help.monsterasp.net/books/github (раздел «Deploy via GitHub Actions»).
> Если их docs предлагают свой готовый workflow — можно взять его и просто вписать те же 4 секрета.

### Шаг 3. Настроить базу (чтобы данные не терялись при передеплое)
SQLite-файл по умолчанию (`forik.db`) лежит в папке приложения. При повторном
WebDeploy папка перезаписывается, и файл может удалиться. Два решения:

**Проще всего — положить базу в папку `App_Data`, которую деплой не трогает.**
В панели MonsterASP.NET (File Manager) создайте папку `App_Data`. Затем задайте
строку подключения через переменную окружения сайта (панель → Environment / App Settings):
```
ConnectionStrings__Default = Data Source=App_Data/forik.db
```
Так база переживёт передеплои.

**Либо переключиться на бесплатную MSSQL от MonsterASP.NET** (надёжнее всего):
1. В панели создайте MSSQL-базу, скопируйте connection string.
2. В проект добавьте пакет:
   ```bash
   dotnet add src/ForikAuction package Microsoft.EntityFrameworkCore.SqlServer
   ```
3. В `Program.cs` замените:
   ```csharp
   o.UseSqlite(builder.Configuration.GetConnectionString("Default"))
   ```
   на:
   ```csharp
   o.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
   ```
4. В переменных окружения сайта задайте `ConnectionStrings__Default` = строку от MSSQL.
   (Модель на EF Core, `EnsureCreated()` создаст таблицы автоматически.)

### Шаг 4. Прописать секреты Google прямо на хостинге
В панели сайта (Environment / App Settings) добавьте:
```
Authentication__Google__ClientId     = ВАШ_CLIENT_ID
Authentication__Google__ClientSecret = ВАШ_CLIENT_SECRET
```
(Двойное подчёркивание `__` — это вложенность конфигурации ASP.NET.)

### Шаг 5. Поправить redirect URI в Google
Google Cloud Console → ваш OAuth-клиент (Web application) → **Authorized redirect URIs**,
добавьте боевой адрес:
```
https://имя.runasp.net/signin-google
```
Сохраните.

### Шаг 6. Запустить деплой
Любой коммит в `main` запускает деплой. Вручную — вкладка **Actions** в GitHub →
workflow «Deploy to MonsterASP.NET» → **Run workflow**. После успешного прогона
открывайте `https://имя.runasp.net`, входите через Google, создавайте комнату и
зовите друга по коду.

---

## Как я дальше дорабатываю код

Рабочий цикл такой:
1. Я правлю файлы в папке проекта (`D:\ClaudeWorks\ForikAuction`).
2. Вы (или я командой) коммитите и пушите:
   ```bash
   git add .
   git commit -m "что изменили"
   git push
   ```
3. GitHub Actions автоматически собирает и публикует новую версию на хостинг.

То есть деплой завязан на GitHub — мне достаточно менять код в репозитории, остальное
происходит само. Откат на любую версию — через `git revert`/возврат коммита и пуш.

---

## Вариант 2. Render (через Docker) — запасной

Подходит, если хотите container-деплой. Важно: **бесплатный диск Render эфемерный**,
поэтому SQLite-файл будет теряться — нужно перейти на внешнюю БД (бесплатный Postgres
Render живёт 30 дней, либо любой внешний Postgres).

1. Код — на GitHub (как в шаге 0 выше). В репозитории уже есть `Dockerfile`.
2. На https://render.com → **New → Web Service** → подключите репозиторий.
3. Render сам увидит `Dockerfile`. Регион — любой; Instance Type — **Free**.
4. Переменные окружения (Environment):
   ```
   ASPNETCORE_URLS = http://0.0.0.0:8080
   Authentication__Google__ClientId     = ...
   Authentication__Google__ClientSecret = ...
   ConnectionStrings__Default = <строка к Postgres>
   ```
5. Для Postgres: переключите код на Npgsql (как в шаге 3, но пакет
   `Npgsql.EntityFrameworkCore.PostgreSQL` и `o.UseNpgsql(...)`).
6. В Google добавьте redirect URI `https://ваш-сервис.onrender.com/signin-google`.
7. Деплой запускается на каждый пуш в `main`.

Нюансы Render free: сервис «засыпает» после 15 минут без активности и просыпается
~минуту при следующем заходе (для игры с другом это терпимо). WebSocket-подключение
тоже будит сервис.

---

## Частые проблемы

- **redirect_uri_mismatch** — адрес в Google не совпадает с реальным. Добавьте точный
  `https://домен/signin-google`.
- **invalid_client** — неверный ClientId/Secret в переменных окружения хостинга.
- **Данные пропали после обновления** — SQLite-файл лежит в перезаписываемой папке.
  Переместите в `App_Data` (MonsterASP) или используйте внешнюю БД.
- **Колесо не крутится у второго игрока** — провайдер режет WebSocket. На MonsterASP.NET
  и Render WebSocket работает; на Azure Free — нет.
