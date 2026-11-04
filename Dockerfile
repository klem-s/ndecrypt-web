FROM mcr.microsoft.com/dotnet/sdk:10.0 AS ndecrypt-build
WORKDIR /src
RUN git clone --depth 1 https://github.com/SabreTools/NDecrypt.git .
RUN dotnet publish NDecrypt/NDecrypt.csproj -c Release -f net8.0 -r linux-x64 \
    --self-contained true -p:PublishSingleFile=true -o /out

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS web-build
WORKDIR /src
COPY web/ .
RUN dotnet publish NDecryptWeb.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=ndecrypt-build /out/NDecrypt /opt/ndecrypt/NDecrypt
RUN chmod +x /opt/ndecrypt/NDecrypt
COPY --from=web-build /out .
ENV NDECRYPT_BIN=/opt/ndecrypt/NDecrypt
ENV LIBRARY_ROOT=/library
ENV OUTPUT_ROOT=/output
ENV HOME=/root
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NDecryptWeb.dll"]
