# Используем базовый образ для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проекта и восстанавливаем зависимости
COPY ["TgBotCursor.csproj", "./"]
RUN dotnet restore

# Копируем все остальные файлы и собираем приложение
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# Устанавливаем Python3, pip и sshpass
RUN apt-get update && \
    apt-get install -y python3 python3-pip sshpass && \
    python3 -m pip install --upgrade pip && \
    python3 -m pip install ansible --break-system-packages

# Создаем директорию для Ansible плейбуков
RUN mkdir -p /app/ansible-deploy

# Копируем собранное приложение
COPY --from=build /app/publish .

# Указываем точку входа
ENTRYPOINT ["dotnet", "TgBotCursor.dll"]