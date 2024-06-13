# DirectShow.NET Webcam Barcode Reader
This repository provides a C# demonstration for creating barcode reader applications on Windows platforms, utilizing [DirectShow.NET](https://directshownet.sourceforge.net/docs.html) for webcam access and [Dynamsoft Barcode Reader SDK](https://www.dynamsoft.com/barcode-reader/sdk-desktop-server/) for barcode decoding.

## Getting Started
1. Obtain a [30-day free trial license](https://www.dynamsoft.com/customer/license/trialLicense) of Dynamsoft Barcode Reader SDK, and set the license key in `Form1.cs`:

    [v9.x](./examples/9.x/Form1.cs)
    ```csharp
    EnumErrorCode errorCode = Dynamsoft.DBR.BarcodeReader.InitLicense("LICENSE-KEY", out errorMsg);
    ```

    [v10.x](./examples/10.x/Form1.cs)

    ```csharp
    int errorCode = LicenseManager.InitLicense("LICENSE-KEY", out errorMsg);
    ```
   
3. Build and run the project using Visual Studio.

    ![DirectShow.NET Webcam barcode reader](http://www.dynamsoft.com/codepool/img/2024/06/directshow-webcam-dotnet-windows-barcode-reader.jpg)

## Blog
[C# Webcam Barcode Reader: A DirectShow.NET Implementation](https://www.dynamsoft.com/codepool/directshow-dotnet-webcam-read-barcode.html)
