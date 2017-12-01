# Continous deployment script
cd ~/formatik/formatik-lib/
git remote update

LIB_LOCAL=$(git rev-parse @)
LIB_REMOTE=$(git rev-parse @{u})
LIB_BASE=$(git merge-base @ @{u})

cd ../formatik-test/
git remote update

TEST_LOCAL=$(git rev-parse @)
TEST_REMOTE=$(git rev-parse @{u})
TEST_BASE=$(git merge-base @ @{u})

cd ../formatik-api/
git remote update

API_LOCAL=$(git rev-parse @)
API_REMOTE=$(git rev-parse @{u})
API_BASE=$(git merge-base @ @{u})

if [[ $LIB_LOCAL = $LIB_REMOTE && $TEST_LOCAL = $TEST_REMOTE && $API_LOCAL = $API_REMOTE && $1 != "force" ]]; then
    echo "Up-to-date"
elif [[ $LIB_LOCAL = $LIB_BASE || $TEST_LOCAL = $TEST_BASE || $API_LOCAL = $API_BASE || $1 == "force" ]]; then
    echo "Rebuilding..."
    
    cd ../formatik-lib/
    git pull

    cd ../formatik-test/
    git pull
    
    sudo rm -r TestResults

    cd ../formatik-api/
    git pull

    # Restores need to be executed in every container. 
    # Restores from prior containers or the host are not valid inside a new container
    
    #run unit Tests
    echo "Testing latest source..."
    docker run \
        --rm \
        -v ~/formatik:/var/formatik \
        -w /var/formatik \
        -c 512 \
        microsoft/dotnet:2.0.3-sdk \
        /bin/bash -c "cd formatik-lib; rm -r bin; rm -r obj; dotnet restore; cd ../formatik-test; rm -r bin; rm -r obj; dotnet restore; dotnet test -c release -l trx;LogFileName=result.trx"

    TEST=$(grep -Po "(?<=<ResultSummary outcome=\")[^\"]+" ../formatik-test/TestResults/*.trx)

    if [[ $TEST == "Completed" ]]; then
        echo "...Tests Completed"
        
        echo "Building API..."
        docker run \
            --rm \
            -v ~/formatik:/var/formatik \
            -w /var/formatik \
            -c 512 \
            microsoft/dotnet:2.0.3-sdk \
            /bin/bash -c "cd formatik-lib; rm -r bin; rm -r obj; dotnet restore; cd ../formatik-api; rm -r bin; rm -r obj; dotnet restore; dotnet publish -c release"

        sudo chmod o+rw -R bin

        echo "...Build complete"

        echo "Building new API Docker image..."
        cp Dockerfile bin/release/netcoreapp2.0/publish/

        cd bin/release/netcoreapp2.0/publish/
        
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
        #     --name api \
        #     --hostname formatik-api \
        #     octagon.formatik.api:latest

        #docker run --rm -ti --name api-test octagon.formatik.api:latest

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

