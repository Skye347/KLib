FROM microsoft/dotnet:1.0.1-core
ARG source
WORKDIR /app
COPY ${source:-bin/Release/PublishOutput} .
ENTRYPOINT ["dotnet", "KLib.Echo.dll"]
