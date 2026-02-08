# 🌍 Production Deployment Guide

## Overview

دليل شامل لنشر التطبيق في بيئة Production.

---

## 🚀 Option 1: Linux/Mac Server

### Set Environment Variables:

```bash
# إنشاء ملف للـ Environment Variables
sudo nano /etc/environment

# أضف المتغيرات:
ConnectionStrings__DefaultConnection="Server=prod-server;Database=ISPManagementDB;User Id=sa;Password=P@ssw0rd!2024;TrustServerCertificate=True;"
JWT__Key="YourProductionJWTKeyThatIsVerySecureAndLong1234567890"
JWT__Issuer="ISPManagementSystem"
JWT__Audience="ISPManagementSystemUsers"
ASPNETCORE_ENVIRONMENT="Production"

# حفظ: Ctrl+X → Y → Enter

# إعادة تحميل
source /etc/environment

# التحقق
echo $JWT__Key
```

### أو استخدم systemd service:

```bash
sudo nano /etc/systemd/system/isp-api.service
```

```ini
[Unit]
Description=ISP Management API
After=network.target

[Service]
Type=notify
WorkingDirectory=/var/www/isp-api
ExecStart=/usr/bin/dotnet /var/www/isp-api/ISP.API.dll

Environment="ConnectionStrings__DefaultConnection=Server=..."
Environment="JWT__Key=YourProductionKey..."
Environment="ASPNETCORE_ENVIRONMENT=Production"

Restart=always
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable isp-api
sudo systemctl start isp-api
sudo systemctl status isp-api
```

---

## 🚀 Option 2: Windows Server

### PowerShell (Permanent):

```powershell
# Set System Environment Variables
[System.Environment]::SetEnvironmentVariable(
    "ConnectionStrings__DefaultConnection",
    "Server=prod-server;Database=ISPManagementDB;...",
    "Machine"
)

[System.Environment]::SetEnvironmentVariable(
    "JWT__Key",
    "YourProductionKey...",
    "Machine"
)

# التحقق
[System.Environment]::GetEnvironmentVariable("JWT__Key", "Machine")

# Restart Service
Restart-Service W3SVC
```

---

## 🚀 Option 3: Azure App Service

### Azure Portal:

```
1. App Service → Configuration
2. Application Settings → + New
3. Add:
   Name: JWT__Key
   Value: YourProductionKey...
4. Save → Restart
```

### Azure CLI:

```bash
az webapp config appsettings set \
  --resource-group ISP-RG \
  --name isp-api \
  --settings \
    JWT__Key="..." \
    ConnectionStrings__DefaultConnection="..."
```

---

## 🚀 Option 4: Docker

### docker-compose.yml:

```yaml
version: "3.8"

services:
  isp-api:
    image: isp-api:latest
    ports:
      - "80:80"
    environment:
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION}
      - JWT__Key=${JWT_SECRET_KEY}
      - ASPNETCORE_ENVIRONMENT=Production
    restart: always
```

### .env file:

```env
DB_CONNECTION=Server=db;Database=ISPManagementDB;...
JWT_SECRET_KEY=YourProductionKey...
```

```bash
docker-compose up -d
```

---

## 🔐 Security Checklist

- [ ] Secrets في Environment Variables (لا في appsettings.json)
- [ ] .env في .gitignore
- [ ] JWT Key قوي (48+ chars)
- [ ] Database Password قوي
- [ ] HTTPS enabled
- [ ] Firewall configured

---

## 🧪 Testing

### Local Test:

```bash
export JWT__Key="TestKey123..."
export ConnectionStrings__DefaultConnection="Server=..."

cd src/ISP.API
dotnet run
```

---

## 📞 Support

Check logs:

```bash
# Linux
journalctl -u isp-api -n 50

# Docker
docker logs isp-api
```
