# Сборка установщика LogCollectorApp

Проект подготовлен под сборку Windows-установщика через Inno Setup.

## Что получится

После сборки появится файл:

```text
dist\LogCollectorApp_Setup_1.0.0.exe
```

Этот установщик поставит приложение в:

```text
%LOCALAPPDATA%\Programs\LogCollectorApp
```

Права администратора не требуются. Локальные настройки приложения, включая SSH-логин и SSH-пароль, сохраняются отдельно:

```text
%APPDATA%\LogCollectorApp\settings.json
```

## Что нужно установить на компьютере сборки

1. .NET SDK, подходящий под проект. Сейчас в `LogCollectorApp.csproj` указан:

```xml
<TargetFramework>net9.0-windows</TargetFramework>
```

Значит, для сборки нужен .NET 9 SDK.

2. Inno Setup 6.

## Как собрать

Открой PowerShell в корне проекта и выполни:

```powershell
.\build-installer.ps1
```

Или запусти двойным кликом:

```text
build-installer.cmd
```

## Что делает скрипт

1. Восстанавливает NuGet-пакеты:

```powershell
dotnet restore
```

2. Публикует приложение как self-contained Windows x64 build:

```powershell
dotnet publish .\LogCollectorApp.csproj -c Release -r win-x64 --self-contained true
```

3. Собирает установщик через Inno Setup Compiler `ISCC.exe`.

## Важный момент про .NET Runtime

Публикация выполняется с параметром:

```text
--self-contained true
```

Это значит, что на компьютере пользователя не нужно отдельно устанавливать .NET Runtime. Все необходимые runtime-файлы попадут внутрь опубликованной сборки и установщика.

## Что не удаляется при деинсталляции

При удалении приложения файл настроек не удаляется автоматически:

```text
%APPDATA%\LogCollectorApp\settings.json
```

Это сделано специально, чтобы пользователь не потерял путь выгрузки и SSH-настройки при переустановке.

## Если сборка падает

### Ошибка: `dotnet не найден`

Установи .NET SDK и перезапусти PowerShell.

### Ошибка: `ISCC.exe не найден`

Установи Inno Setup 6. Скрипт ищет компилятор по стандартным путям:

```text
C:\Program Files (x86)\Inno Setup 6\ISCC.exe
C:\Program Files\Inno Setup 6\ISCC.exe
```

### Ошибка из-за TargetFramework

Если на компьютере установлен только .NET 8 SDK, можно заменить в `.csproj`:

```xml
<TargetFramework>net9.0-windows</TargetFramework>
```

на:

```xml
<TargetFramework>net8.0-windows</TargetFramework>
```

Для этого проекта это безопаснее, потому что зависимости EF Core уже версии 8.0.0.
