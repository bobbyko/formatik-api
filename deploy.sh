# Continous deployment script
git remote update

LOCAL=$(git rev-parse @)
REMOTE=$(git rev-parse @{u})
BASE=$(git merge-base @ @{u})

if [[ $LOCAL = $REMOTE && $1 != "force" ]]; then
    echo "Up-to-date"
elif [[ $LOCAL = $BASE || $1 == "force" ]]; then
    echo "Rebuilding..."
    
    git pull

    sudo rm -r Tests/TestResults

    # Restores need to be executed in every container. 
    # Restores from prior containers or the host are not valid inside a new container
    
    #run unit Tests
    echo "Testing latest source..."
    docker run \
        --rm \
        -v ~/Formatik-v0.1:/var/Formatik-v0.1 \
        -w /var/Formatik-v0.1 \
        -c 512 \
        microsoft/dotnet:1.1.2-sdk-1.0.4 \
        /bin/bash -c "cd Formatik; dotnet restore; cd ../Tests; dotnet restore; dotnet test -c release -l trx;LogFileName=result.trx"

    TEST=$(grep -Po "(?<=<ResultSummary outcome=\")[^\"]+" Tests/TestResults/*.trx)

    if [[ $TEST == "Completed" ]]; then
        echo "...Tests Completed"
        
        echo "Building API..."
        docker run \
            --rm \
            -v ~/Formatik-v0.1:/var/Formatik-v0.1 \
            -w /var/Formatik-v0.1 \
            -c 512 \
            microsoft/dotnet:1.1.2-sdk-1.0.4 \
            /bin/bash -c "cd Formatik; dotnet restore; cd ../API; dotnet restore; dotnet publish -c release"

        sudo chmod o+rw -R API/bin

        echo "...Build complete"

        echo "Building new API Docker image..."
        cp Production/Dockerfile API/bin/release/netcoreapp1.1/publish/

        cd API/bin/release/netcoreapp1.1/publish/
        
        docker rmi octagon.formatik.api:old
        docker tag octagon.formatik.api:latest octagon.formatik.api:old
        docker build --tag=octagon.formatik.api:latest .

        echo "...image build complete"

        echo "Updating API service..."

        # For new swarms create service manually like this
        # docker service create \
        #     --network formatik_net \
        #     --replicas 1 \
        #     --constraint 'node.labels.api == true' \
        #     --publish 80:8000 \
        #     --host mongo01:10.134.22.243 \
        #     --name api \
        #     --hostname formatik-api \
        #     octagon.formatik.api:latest

        #docker run --rm -ti -p 8000:8000 --add-host mongo01:10.134.22.243 --name api-test octagon.formatik.api:latest

        docker service update \
            --image octagon.formatik.api:latest \
            --force \
            api

        echo "...API service updated"

        curl -s --user 'api:key-0f66fb27e1d888d8d5cddaea7186b634' \
            https://api.mailgun.net/v3/sandboxf5c90e4cf7524486831c10e8d6475ebd.mailgun.org/messages \
                -F from='Formatik01 <postmaster@sandboxf5c90e4cf7524486831c10e8d6475ebd.mailgun.org>' \
                -F to='Bobby Kotzev <bobby@octagonsolutions.co>' \
                -F subject='Successfully updated Formatik API' \
                -F text='...using latest source from master branch'
    else
        echo "...Tests Failed"

        curl -s --user 'api:key-0f66fb27e1d888d8d5cddaea7186b634' \
            https://api.mailgun.net/v3/sandboxf5c90e4cf7524486831c10e8d6475ebd.mailgun.org/messages \
                -F from='Formatik01 <postmaster@sandboxf5c90e4cf7524486831c10e8d6475ebd.mailgun.org>' \
                -F to='Bobby Kotzev <bobby@octagonsolutions.co>' \
                -F subject='Failed to update Formatik API' \
                -F text='...latest source from master branch failed validation'
    fi
fi

