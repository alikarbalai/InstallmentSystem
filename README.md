# نظام إدارة الأقساط - ASP.NET Core API

## متطلبات التشغيل
- .NET 8 SDK
- SQL Server (أو SQL Server Express)

## خطوات التشغيل

```bash
# 1. استعادة الحزم
dotnet restore

# 2. إنشاء Migration
dotnet ef migrations add InitialCreate

# 3. تطبيق قاعدة البيانات
dotnet ef database update

# 4. تشغيل المشروع
dotnet run
```

## Endpoints الرئيسية

### العملاء
- GET    /api/customers
- GET    /api/customers/{id}
- GET    /api/customers/{id}/statement  (كشف حساب)
- POST   /api/customers
- PUT    /api/customers/{id}
- DELETE /api/customers/{id}

### العقود
- GET    /api/contracts
- GET    /api/contracts/{id}
- GET    /api/contracts/dashboard  (إحصائيات)
- POST   /api/contracts
- PATCH  /api/contracts/{id}/cancel

### الأقساط
- GET    /api/installments/contract/{contractId}
- GET    /api/installments/overdue   (المتأخرة)
- GET    /api/installments/upcoming  (القادمة خلال 7 أيام)
- POST   /api/installments/update-overdue

### الدفعات
- GET    /api/payments
- GET    /api/payments/contract/{contractId}
- POST   /api/payments

### المنتجات
- GET    /api/products
- GET    /api/products/{id}
- POST   /api/products
- PUT    /api/products/{id}
- DELETE /api/products/{id}

## Swagger UI
بعد التشغيل: http://localhost:5000/swagger
