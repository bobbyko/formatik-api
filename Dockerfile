FROM microsoft/dotnet:1.1.2-runtime
MAINTAINER Bobby Kotzev

ENV TZ=America/Los_Angeles
ENV appDir /srv/Formatik/v0.1/API

RUN mkdir -p ${appDir}
WORKDIR ${appDir}

COPY . ${appDir}

#set asp to listen on port 8000, any IP request
ENV ASPNETCORE_URLS=http://*:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "Octagon.Formatik.API.dll"]