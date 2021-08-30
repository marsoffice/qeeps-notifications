FROM mcr.microsoft.com/dotnet/sdk:3.1-focal
RUN wget -q https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get update
RUN apt-get install -y azure-functions-core-tools-3 curl