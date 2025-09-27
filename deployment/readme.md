# Deployment Instructions

Execute `main.bicep`: it will create all the needed resources.

## Configure Solution Deployment

1. Go to **Azure Portal** > **Azure Functions** > **proximity-api** and download the publishing profile **(x)**
2. Go to **Azure Portal** > **Static Web Apps** > **proximity** and download the publishing profile **(y)**

Copy the publishing profiles to **GitHub** > **\<your-org\>** > **proximity** > **Settings** > **Secrets and variables** > **Actions** > **Repository secrets**:

* `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`: **(x)**
* `AZURE_STATIC_WEB_APPS_API_TOKEN`: **(y)**

