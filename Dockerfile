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

# Устанавливаем Python3, python3-pip, sshpass и openssh-client, минимизируя зависимости
RUN apt-get update && \
    apt-get install -y --no-install-recommends python3 python3-pip sshpass openssh-client && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    ln -s /usr/bin/python3 /usr/bin/python

# Устанавливаем Ansible через pip без кэша с обходом ограничений
RUN pip3 install --no-cache-dir ansible --break-system-packages

# Копируем собранное приложение
COPY --from=build /app/publish .

# Копируем содержимое каталога ansible-bot
RUN mkdir -p /app/ansible-bot/ssh
COPY ansible-bot/ /app/ansible-bot/

# Сохранение ключа
COPY ansible_key.txt /app/ansible-bot/ssh/ansible_key
RUN chmod 600 /app/ansible-bot/ssh/ansible_key

# Указываем точку входа
ENTRYPOINT ["dotnet", "TgBotCursor.dll"]