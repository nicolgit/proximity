This file contains the commands to generate production and test data for metro-proximity

# production

```bash
dotnet run -- area create naples --center 40.8585186,14.2543934 --diameter 21000 --displayname "Napoli" 
dotnet run -- area create rome --center 41.8902142,12.489656 --diameter 50000 --displayname "Roma" 
dotnet run -- area create milan --center 45.4627338,9.1777322 --diameter 15000 --displayname "Milano" 
dotnet run -- area create turin --center 45.0694185,7.661424 --diameter 15000 --displayname "Torino"
dotnet run -- area create florence --center 43.771389,11.254167 --diameter 15000 --displayname "Firenze" 
dotnet run -- area create bologna --center 44.495054,11.3415394 --diameter 15000 --displayname "Bologna"

```

# test

```bash


dotnet run -- area create naples --center 40.8585186,14.2543934 --diameter 20000 --displayname "Napoli" --developer --noisochrone --logging debug
dotnet run -- area create rome --center 41.8902142,12.489656 --diameter 45000 --displayname "Roma" --developer --noisochrone --logging debug
dotnet run -- area create milan --center 45.4627338,9.1777322 --diameter 15000 --displayname "Milano" --developer --noisochrone --logging debug

dotnet run -- area create bologna --center 44.495054,11.3415394 --diameter 12000 --displayname "Bologna (test)" --developer
dotnet run -- area create padua --center 45.407778,11.873333 --diameter 10000 --displayname "Padova (test)" --noisochrone
```