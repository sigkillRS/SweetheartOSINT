# Публикация на GitHub и создание релиза

Этот файл — пошаговая инструкция. В текущем окружении `git` и `gh` не установлены и URL репозитория не задан, поэтому команды ниже нужно выполнить у себя после установки Git.

## 1. Что уже подготовлено в папке

- `README.md` — описание приложения и сборки.
- `RELEASE_NOTES.md` — готовый текст для релиза.
- `docs/screenshots/` — скриншоты.
- `SweetheartOSINT/` — исходники .NET 8 / WPF.
- `.gitignore` — исключает `bin/`, `obj/`, `publish/`, `Projects/`, `profile.json`, `avatar.*`, логи и файлы IDE/OS.

Папки `SweetheartOSINT\bin` и `SweetheartOSINT\obj` удалены как мусор сборки. Старые проекты из `publish\win-x64\Projects` удалены по вашему указанию; папка `Projects` создастся заново при первом запуске приложения. Готовый `publish\win-x64\SweetheartOSINT.exe` оставлен на месте — он нужен для релиза. **Не добавляйте `publish` в коммит.**

Скриншоты `main.png` и `editor.png` пересняты с пустого/нейтрального состояния. Файл `docs\screenshots\project.png` в `README` не используется и добавлен в `.gitignore`, чтобы старые данные не попали в репозиторий.

## 2. Создайте пустой репозиторий на GitHub

1. Откройте <https://github.com/new>.
2. **Repository name:** `SweetheartOSINT`.
3. **Description:** `Локальное WPF-приложение для OSINT-проектов (.NET 8, x64)`.
4. Выберите **Public** или **Private**.
5. **НЕ** ставьте галочки `Add a README file`, `Add .gitignore`, `Choose a license` — локальные файлы уже есть.
6. Нажмите **Create repository**.

Скопируйте показанный GitHub URL вида `https://github.com/<ВАШ_ЛОГИН>/SweetheartOSINT.git`.

## 3. Залейте исходники через Git

В PowerShell перейдите в папку проекта и выполните по очереди:

```powershell
cd "C:\Users\barby\OneDrive\Desktop\osint-tools"

git init -b main
git add .
git status --short
git commit -m "Initial commit: Sweetheart OSINT"

git remote add origin https://github.com/<ВАШ_ЛОГИН>/SweetheartOSINT.git
git push -u origin main
```

В `git status --short` должны появиться только:

```text
.gitignore
GITHUB.md
README.md
RELEASE_NOTES.md
docs/screenshots/main.png
docs/screenshots/editor.png
SweetheartOSINT/...
```

Не должно быть `publish/`, `bin/`, `obj/`, `Projects/`, `profile.json`, `avatar.*`. Если что-то из этого появилось — удалите из индекса:

```powershell
git rm -r --cached publish SweetheartOSINT/bin SweetheartOSINT/obj
git commit -m "Remove generated files"
```

Если ваша версия Git старая и не поддерживает `git init -b main`:

```powershell
git init
git branch -M main
```

### Вариант без Git (через сайт GitHub)

После создания пустого репозитория нажмите **Add file → Upload files** и загрузите **только**:

```text
.gitignore
README.md
GITHUB.md
RELEASE_NOTES.md
docs\screenshots\main.png
docs\screenshots\editor.png
SweetheartOSINT\
```

Папку `publish\`, любые папки `Projects\`, а также `docs\screenshots\project.png` не загружайте — веб-загрузка не учитывает `.gitignore`.

## 4. Создайте Release

1. Откройте репозиторий на GitHub → справа **Releases** → **Create a new release**.
2. **Choose a tag:** введите `v1.0.0` и нажмите `Create new tag: v1.0.0 on publish`.
3. **Target:** `main`.
4. **Release title:** `Sweetheart OSINT v1.0.0`.
5. В поле описания вставьте содержимое файла `RELEASE_NOTES.md`.
6. В блок **Attach binaries** перетащите файл:

```text
C:\Users\barby\OneDrive\Desktop\osint-tools\publish\win-x64\SweetheartOSINT.exe
```

7. Галочку **Set as a pre-release** не ставьте.
8. Нажмите **Publish release**.

Необязательно: можно приложить ZIP вместо exe:

```powershell
Compress-Archive -Path 'publish\win-x64\SweetheartOSINT.exe','README.md' -DestinationPath 'SweetheartOSINT-v1.0.0-win-x64.zip' -Force
```

### Вариант через GitHub CLI

После `gh auth login`:

```powershell
gh release create v1.0.0 publish\win-x64\SweetheartOSINT.exe --title "Sweetheart OSINT v1.0.0" --notes-file RELEASE_NOTES.md
```

## 5. Проверка после публикации

- На главной странице репозитория должен отображаться `README.md` со скриншотами.
- В разделе **Releases** должна быть запись `v1.0.0` с вложением `.exe` (или `.zip`).
- В дереве файлов не должно быть `publish/`, `Projects/`, `profile.json` и картинок пользовательских проектов.
