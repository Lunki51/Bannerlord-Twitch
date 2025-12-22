$(document).ready(function () {
    const container = document.getElementById('campaign-map-container');
    const svg = document.getElementById('campaign-map-svg');
    const settlementMarkersGroup = document.getElementById('settlement-markers');
    const tooltip = document.getElementById('map-tooltip');
    const tooltipName = document.getElementById('tooltip-name');
    const tooltipInfo = document.getElementById('tooltip-info');

    let currentMapData = null;
    let kingdomColors = new Map();
    let isConnected = false;

    // Use SignalR if available
    if (typeof $.connection.mapHub !== 'undefined') {
        const mapHub = $.connection.mapHub;

        mapHub.client.updateMap = function (mapData) {
            if (!mapData) {
                // Hide the map when in mission or no data
                container.classList.add('hidden');
                currentMapData = null;
                return;
            }

            // Show the map and render data
            container.classList.remove('hidden');
            currentMapData = mapData;
            renderMap(mapData);
        };

        $.connection.hub.start().done(() => {
            console.log('Campaign Map Hub connected');
            isConnected = true;
            mapHub.server.refresh();
            
            // Poll for updates every 2 seconds to catch mission changes
            setInterval(() => {
                if (isConnected) {
                    mapHub.server.refresh();
                }
            }, 2000);
        });

        $.connection.hub.disconnected(() => {
            isConnected = false;
            console.log('Campaign Map Hub disconnected');
        });

        $.connection.hub.reconnected(() => {
            isConnected = true;
            console.log('Campaign Map Hub reconnected');
            mapHub.server.refresh();
        });
    } else {
        // Testing mode - hide by default
        container.classList.add('hidden');
    }

    function renderMap(data) {
        currentMapData = data;
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
                shape.setAttribute('r', 1.2); // Adjusted for viewBox 100x30
            } else { // Castle
                shape = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                shape.setAttribute('x', -1);
                shape.setAttribute('y', -1);
                shape.setAttribute('width', 2);
                shape.setAttribute('height', 2); // Adjusted for viewBox 100x30
            }

            shape.setAttribute('fill', color);
            shape.setAttribute('stroke', '#fff');
            shape.setAttribute('stroke-width', 0.2);
            group.appendChild(shape);

            group.addEventListener('mouseenter', e => showTooltip(e, settlement));
            group.addEventListener('mousemove', e => updateTooltipPosition(e));
            group.addEventListener('mouseleave', hideTooltip);

            settlementMarkersGroup.appendChild(group);
        });
    }

    function showTooltip(e, settlement) {
        const kingdom = currentMapData?.Kingdoms?.find(k => k.Id === settlement.KingdomId);
        tooltipName.textContent = settlement.Name;
        tooltipInfo.innerHTML = `${settlement.Type}<br>Kingdom: ${kingdom ? kingdom.Name : 'Neutral'}`;
        tooltip.style.display = 'block';
        updateTooltipPosition(e);
    }

    function updateTooltipPosition(e) {
        tooltip.style.left = (e.clientX + 10) + 'px';
        tooltip.style.top = (e.clientY - 10) + 'px';
    }

    function hideTooltip() {
        tooltip.style.display = 'none';
    }
});