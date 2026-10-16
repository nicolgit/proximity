# Proximity Azure SRE Agent

This folder contains the Bicep deployment for an Azure SRE Agent that monitors the Proximity solution.

## Architecture

- The SRE Agent is deployed to the `proximity-sre` resource group in **Italy North**.
- The agent monitors all resources in the existing `proximity-playground` resource group with read-only access.
- Agent telemetry is stored in a dedicated Log Analytics workspace and Application Insights resource.
- Outbound agent traffic is integrated with the existing `proximity-net/sre-subnet` subnet.
- The subnet is assumed to be in `proximity-playground`. Override `networkResourceGroupName` at deployment time if it is stored in another resource group.
- The GitHub repository `https://github.com/nicolgit/proximity` must be added to the agent Knowledge Base after the infrastructure deployment.

## Prerequisites

Before deploying:

1. The `proximity-playground` resource group and all Proximity resources must be fully configured.
2. The `proximity-net` virtual network and `sre-subnet` subnet must already exist.
3. `sre-subnet` must:
   - be in Italy North;
   - use a `/27` or larger address range;
   - be dedicated to the SRE Agent;
   - be delegated to `Microsoft.App/environments`.
4. The deployment identity must have:
   - permission to create resource groups and resources in the subscription;
   - `Owner`, or both `Contributor` and `User Access Administrator`, so that the deployment can create role assignments.
5. The subscription must be enabled for Azure SRE Agent in Italy North.
6. Azure CLI and Bicep must be installed.

## Validate the deployment

From this folder, run the following command (compatible with PowerShell and Bash):

```powershell
az deployment sub what-if --name proximity-sre-preview --location italynorth --template-file main-sre.bicep
```

## Deploy

```bash
az deployment sub create --name proximity-sre --location italynorth --template-file main-sre.bicep
```

The deployment outputs include direct links to the SRE Agent and its resource group.

## Add the GitHub repository to the Knowledge Base

Code repository connections require GitHub OAuth or a Personal Access Token and are currently configured through the Azure SRE Agent data plane rather than ARM/Bicep.

After deployment:

1. Open the agent using the `agentPortalUrl` deployment output.
2. Go to **Builder** > **Knowledge base**.
3. Select **Add repository**.
4. Select **GitHub** and authenticate with OAuth, or use a Personal Access Token with `repo` scope.
5. Add `https://github.com/nicolgit/proximity`.
6. Save the Knowledge Base and verify that repository indexing completes successfully.

The Bicep configuration enables GitHub traffic through the Azure SRE Agent infrastructure network while the agent is in Azure VNet mode.

## Remove the SRE Agent

Deleting `proximity-sre` removes the agent and all supporting resources created by this deployment:

```bash
az group delete --name proximity-sre --yes
```

The existing `proximity-playground` resource group, virtual network, subnet, and GitHub repository are not deleted.
