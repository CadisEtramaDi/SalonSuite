# SalonSuite — Cloud Deployment Guide

SalonSuite is a desktop-oriented salon appointment management system developed using ASP.NET Core / .NET 10 and deployed to Amazon Web Services (AWS).

## 1. Cloud Deployment Overview

- **Application:** ASP.NET Core / .NET 10
- **Hosting:** Amazon EC2
- **Operating System:** Ubuntu
- **Instance Type:** t3.micro
- **AWS Region:** `ap-southeast-2` (Asia Pacific – Sydney)
- **Application Port:** `127.0.0.1:5261`
- **Public Domain:** `https://salonsuite.ddns.net`
- **Source Repository:** `https://github.com/CadisEtramaDi/SalonSuite.git`
- **Reverse Proxy / HTTPS:** Nginx
- **Authentication:** Firebase Authentication
- **Database:** Firebase Firestore
- **Cloud Object Storage:** Amazon S3

## 2. System Architecture

```text
User (Web Browser)
        |
     Internet
        |
Nginx Reverse Proxy / HTTPS
        |
ASP.NET Core MVC
    SalonSuite
     /           /         Firebase     Firestore
Authentication
```

Nginx provides the public HTTPS entry point and forwards requests to SalonSuite on the local application port. Firebase Authentication and Firestore are separate services used by the application.

## 3. Deploying SalonSuite to AWS EC2

### Step 1: Connect to the EC2 Instance

```bash
ssh ubuntu@<EC2-PUBLIC-IP>
```

### Step 2: Clone the Repository

```bash
git clone https://github.com/CadisEtramaDi/SalonSuite.git /home/ubuntu/SalonSuite
cd /home/ubuntu/SalonSuite
```

### Step 3: Restore Dependencies

```bash
dotnet restore
```

### Step 4: Build the Application

```bash
dotnet build SalonSuite.csproj -c Release
```

## 4. Running SalonSuite with systemd

For production, SalonSuite is managed by `systemd` instead of relying on an SSH or tmux session.

### Step 1: Create the Service

```bash
sudo nano /etc/systemd/system/salonsuite.service
```

Use:

```ini
[Unit]
Description=SalonSuite ASP.NET Core Application
After=network-online.target
Wants=network-online.target

[Service]
WorkingDirectory=/home/ubuntu/SalonSuite
ExecStart=/usr/bin/dotnet /home/ubuntu/SalonSuite/bin/Release/net10.0/SalonSuite.dll --urls http://127.0.0.1:5261
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=salonsuite
User=ubuntu
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

### Step 2: Enable and Start the Service

```bash
sudo systemctl daemon-reload
sudo systemctl enable salonsuite
sudo systemctl start salonsuite
```

### Step 3: Verify the Service

```bash
sudo systemctl status salonsuite --no-pager
```

Expected:

```text
Active: active (running)
```

### Step 4: Verify Port 5261

```bash
sudo ss -ltnp | grep ':5261'
```

SalonSuite should listen on:

```text
127.0.0.1:5261
```

## 5. Nginx Reverse Proxy

Nginx forwards public requests to the local SalonSuite application.

The production domain is:

```text
salonsuite.ddns.net
```

The application runs locally on:

```text
http://127.0.0.1:5261
```

Example Nginx configuration:

```nginx
server {
    listen 80;
    server_name salonsuite.ddns.net;

    location / {
        proxy_pass http://127.0.0.1:5261;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

Test and restart Nginx:

```bash
sudo nginx -t
sudo systemctl restart nginx
```

### Request Flow

```text
https://salonsuite.ddns.net
        |
        v
      Nginx
        |
        v
127.0.0.1:5261
        |
        v
   SalonSuite
```

## 6. HTTPS

SalonSuite is accessed through:

```text
https://salonsuite.ddns.net
```

Nginx provides the public HTTPS entry point while SalonSuite remains accessible only through the local application port.

TLS private keys must not be committed to GitHub.

## 7. Firebase Configuration

### Firebase Project

- **Project ID:** `it20-49faf`
- **Auth Domain:** `it20-49faf.firebaseapp.com`

### Authorized Production Domain

```text
salonsuite.ddns.net
```

The production domain is authorized for Firebase Authentication.

### Security

Do not commit Firebase Admin private keys or service-account credentials to the repository.

## 8. Amazon S3 and IAM

SalonSuite's AWS environment includes:

- **S3 Bucket:** `lupogan-548096`
- **Region:** `ap-southeast-2`
- **EC2 IAM Role:** `S3-bucket-access`

The verified IAM role includes:

- `AmazonS3FullAccess`
- `AmazonSSMManagedInstanceCore`
- `CloudWatchAgentServerPolicy`

AWS credentials should not be stored directly in the application. The EC2 IAM role is used for AWS access.

> **Note:** The existence of the S3 bucket and IAM role does not by itself prove that every SalonSuite feature actively uploads files to S3. Only document specific S3 application features when confirmed in the application source code.

## 9. Security

- Use HTTPS for public access.
- Use the EC2 IAM role instead of long-lived AWS credentials.
- Keep Firebase Admin credentials private.
- Keep TLS private keys private.
- Expose only required ports through the EC2 security group.
- Review IAM permissions and apply least privilege where possible.
- Do not commit secrets or credentials to GitHub.

## 10. Monitoring and Maintenance

### Check SalonSuite

```bash
sudo systemctl status salonsuite --no-pager
```

### Check the Application Port

```bash
sudo ss -ltnp | grep ':5261'
```

### Check Nginx

```bash
sudo nginx -t
sudo systemctl status nginx --no-pager
```

### View Application Logs

```bash
sudo journalctl -u salonsuite -n 50 --no-pager
```

### Check Server Resources

```bash
df -h
free -h
```

Regularly monitor the application, disk space, memory, logs, dependencies, and AWS permissions.

## 11. Deployment Verification Checklist

Before presenting SalonSuite, verify:

- [ ] EC2 instance is running.
- [ ] SalonSuite service is `active (running)`.
- [ ] SalonSuite is listening on `127.0.0.1:5261`.
- [ ] Nginx configuration passes `nginx -t`.
- [ ] Nginx is running.
- [ ] `https://salonsuite.ddns.net` opens successfully.
- [ ] Desktop homepage displays correctly.
- [ ] CSS and JavaScript load correctly.
- [ ] Firebase Authentication works with the production domain.
- [ ] Firestore is accessible by the application.
- [ ] No credentials or private keys are exposed in the repository.

## 12. Production Deployment Flow

```text
GitHub Repository
       |
       v
AWS EC2 (Ubuntu)
       |
       v
systemd
       |
       v
ASP.NET Core / .NET 10
       |
       v
127.0.0.1:5261
       |
       v
Nginx / HTTPS
       |
       v
salonsuite.ddns.net
       |
       +----------------------+
       |                      |
       v                      v
Firebase Authentication    Firestore
```

## Conclusion

SalonSuite is deployed as an ASP.NET Core / .NET 10 application on an Ubuntu Amazon EC2 instance. Nginx provides the public HTTPS reverse-proxy layer, systemd keeps the application running as a persistent service, and Firebase provides authentication and Firestore services. Amazon S3 and an EC2 IAM role are also configured in the AWS environment.

This document contains the essential deployment information and avoids unnecessary infrastructure details and sensitive credentials.
