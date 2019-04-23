var requests = new Vue({
    el: '#requestTable',
    data: {
        permissionRequests: [
            { name: 'repo1', user: 'user1', timestamp: 'right now!' },
            { name: 'repo2', user: 'user2', timestamp: 'idk awhile ago' },
            { name: 'repo3', user: 'user3', timestamp: 'like forever ago!' },
            { name: 'repo4', user: 'user4', timestamp: 'holy crap idk!' },
            { name: 'repo5', user: 'user5', timestamp: 'ugh...yesterday?' }
        ],
        show: true
    }
})

var login = new Vue({
    el: '#userData',
    data: {
        show: true
    }
})