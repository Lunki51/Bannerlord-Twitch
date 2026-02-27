$(document).ready(function () {
    const container = document.getElementById('campaign-map-container');
    const settlementMarkersGroup = document.getElementById('settlement-markers');
    const coastlineGroup = document.getElementById('coastline-layer');
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
                if (isConnected) mapHub.server.refresh();
            }, 2000);
        });

        $.connection.hub.disconnected(() => { isConnected = false; });
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
            data.Kingdoms.forEach(k => kingdomColors.set(k.Id, { fill: k.Color1, border: k.Color2 }));
        }
        renderCoastline(data.Coastline || []);
        renderSettlements(data.Settlements || []);
    }

    function renderCoastline(segments) {
        coastlineGroup.innerHTML = '';
        if (segments.length === 0) return;

        // Build an SVG path from all segments for efficiency (one element vs thousands)
        // Group into connected chains first for smoother rendering
        let d = '';
        segments.forEach(seg => {
            d += `M ${seg.X1.toFixed(2)} ${seg.Y1.toFixed(2)} L ${seg.X2.toFixed(2)} ${seg.Y2.toFixed(2)} `;
        });

        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('d', d);
        path.setAttribute('stroke', '#102066');
        path.setAttribute('stroke-width', '0.5');
        path.setAttribute('stroke-linecap', 'round');
        path.setAttribute('fill', 'none');
        path.setAttribute('opacity', '0.8');
        coastlineGroup.appendChild(path);
    }

    function renderSettlements(settlements) {
        settlementMarkersGroup.innerHTML = '';
        settlements.forEach(settlement => {
            const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            group.setAttribute('class', `settlement-marker ${settlement.Type}`);
            group.setAttribute('transform', `translate(${settlement.X},${settlement.Y})`);

            const kingdom = kingdomColors.get(settlement.KingdomId);
            const fillColor = kingdom ? kingdom.fill : '#888888';
            const borderColor = kingdom ? kingdom.border : '#ffffff';

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
            shape.setAttribute('fill', fillColor);
            shape.setAttribute('stroke', borderColor);
            shape.setAttribute('stroke-width', '0.6');
            group.appendChild(shape);
            settlementMarkersGroup.appendChild(group);
        });
    }
});