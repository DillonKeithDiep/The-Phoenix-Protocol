var serverSocket

// Initialise the web socket connection
function initSocket(resumeInitialisation) {
    if(typeof serverSocket === 'undefined') {
        serverSocket = new WebSocket("ws://localhost:8080/web_socket");

        // After connection initialisation
        serverSocket.onopen = function() {
            // Proceed with page initialisation
            // in init.js
            resumeInitialisation()
            console.log("Web Socket Connection initialised");
        }

        // Receiving from server
        serverSocket.onmessage = function(e) { onMessage(e) }

        // Web Socket Error
        serverSocket.onerror = function(e) {
            conosle.log("Web Socket Error: "+e.data)
        }

        // After closing the connection
        serverSocket.onclose = function() {
            console.log("Web Socket Connection Closed")
        }
    }
}

// Act based on type of command
function onMessage(event) {
    var msg = JSON.parse(event.data);

    // TODO: implement commented functions
    switch (msg.type) {
        case "SAVE_USER":
            // in init.js
            saveUserAndUpdate(msg.data)
            break;
        case "USER_UPDATE":
            // in transition.js
            updateScreen(msg.data)
            break;
        case "STATE_CHANGE":
            //changeState(msg.data)
            break;
        case "STATE_UPDATE":
            //updateState(msg.data)
            break;
        case "STATISTICS_UPDATE":
            //updateStats(msg.data)
            break;
        default:
            console.log("Received unexpected message type: "+msg.type)
    }

}

// Request information for an existing user
function requestUserUpdate() {
    var userId = Cookies.get("user_id")
    var msg = {
        type: "UPDATE_USER",
        data: userId
    }

    serverSocket.send(JSON.stringify(msg));
}

// Request the creation of a new user with the appropriate name
function requestUserCreation(username) {
    var msg = {
        type: "REG_USER",
        data: username
    }

    serverSocket.send(JSON.stringify(msg));
}
