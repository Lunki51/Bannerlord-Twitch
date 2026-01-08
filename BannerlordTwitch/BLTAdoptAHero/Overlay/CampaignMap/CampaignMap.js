$(document).ready(function () {
    const container = document.getElementById('campaign-map-container');
    const settlementMarkersGroup = document.getElementById('settlement-markers');

    let kingdomColors = new Map();
    let isConnected = false;

    if (typeof $.connection.mapHub !== 'undefined') {
        const mapHub = $.connection.mapHub;

        mapHub.client.updateMap = function (mapData) {
            if (!mapData) {
                container.classList.add('hidden');
                return;
            }

            container.classList.remove('hidden');
            renderMap(mapData);
        };

        $.connection.hub.start().done(() => {
            console.log('Campaign Map Hub connected');
            isConnected = true;
            mapHub.server.refresh();

            setInterval(() => {
                if (isConnected) {
                    mapHub.server.refresh();
                }
            }, 2000);
        });

        $.connection.hub.disconnected(() => {
            isConnected = false;
        });

        $.connection.hub.reconnected(() => {
            isConnected = true;
            mapHub.server.refresh();
        });
    } else {
        container.classList.add('hidden');
    }

    function renderMap(data) {
        kingdomColors.clear();
        if (data.Kingdoms) {
            data.Kingdoms.forEach(k => kingdomColors.set(k.Id, k.Color));
        }
        renderSettlements(data.Settlements || []);
    }

    function renderSettlements(settlements) {
        settlementMarkersGroup.innerHTML = '';

        settlements.forEach(settlement => {
            const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            group.setAttribute('class', `settlement-marker ${settlement.Type}`);
            group.setAttribute('transform', `translate(${settlement.X},${settlement.Y})`);

            const color = kingdomColors.get(settlement.KingdomId) || '#888888';

            let shape;
            if (settlement.Type === 'Town') {
                shape = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                shape.setAttribute('cx', 0);
                shape.setAttribute('cy', 0);
                shape.setAttribute('r', 2.65);
            } else {
                shape = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                shape.setAttribute('x', -2);
                shape.setAttribute('y', -2);
                shape.setAttribute('width', 3.5);
                shape.setAttribute('height', 3.5);
            }

            shape.setAttribute('fill', color);
            group.appendChild(shape);
            settlementMarkersGroup.appendChild(group);
        });
    }
});