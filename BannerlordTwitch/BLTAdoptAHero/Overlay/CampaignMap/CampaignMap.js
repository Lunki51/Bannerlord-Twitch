$(document).ready(function () {
    const svg = document.getElementById('campaign-map-svg');
    const terrainZonesGroup = document.getElementById('terrain-zones');
    const settlementMarkersGroup = document.getElementById('settlement-markers');
    const legendItems = document.getElementById('legend-items');
    const tooltip = document.getElementById('map-tooltip');
    const tooltipName = document.getElementById('tooltip-name');
    const tooltipInfo = document.getElementById('tooltip-info');

    let currentMapData = null;
    let kingdomColors = new Map();

    // Example: use SignalR if available
    if (typeof $.connection.mapHub !== 'undefined') {
        const mapHub = $.connection.mapHub;
        mapHub.client.updateMap = function (mapData) {
            if (!mapData) { $('#campaign-map-container').hide(); return; }
            $('#campaign-map-container').show();
            currentMapData = mapData;
            renderMap(mapData);
        }
        $.connection.hub.start().done(() => mapHub.server.refresh());
    }

    function renderMap(data) {
        currentMapData = data;
        kingdomColors.clear();
        if (data.Kingdoms) data.Kingdoms.forEach(k => kingdomColors.set(k.Id, k.Color));
        renderTerrainZones(data.TerrainZones || []);
        renderSettlements(data.Settlements || []);
        updateLegend(data.Kingdoms || []);
    }

    function renderTerrainZones(zones) {
        terrainZonesGroup.innerHTML = '';
        zones.forEach(zone => {
            if (!zone.Points || zone.Points.length < 3) return;
            const polygon = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
            polygon.setAttribute('points', zone.Points.map(p => `${p[0]},${p[1]}`).join(' '));
            polygon.setAttribute('class', 'terrain-zone ' + zone.Type);
            terrainZonesGroup.appendChild(polygon);
            // Label
            const cx = zone.Points.reduce((s, p) => s + p[0], 0) / zone.Points.length;
            const cy = zone.Points.reduce((s, p) => s + p[1], 0) / zone.Points.length;
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', cx); label.setAttribute('y', cy);
            label.setAttribute('class', 'zone-label');
            label.setAttribute('font-size', 2);
            label.textContent = zone.Type === 'sea' ? 'Sea' : zone.Type === 'blocked' ? 'Impassable' : '';
            if (label.textContent) terrainZonesGroup.appendChild(label);
        });
    }

    function renderSettlements(settlements) {
        settlementMarkersGroup.innerHTML = '';
        settlements.forEach(settlement => {
            const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            group.setAttribute('class', `settlement-marker ${settlement.Type}`);
            group.setAttribute('transform', `translate(${settlement.X},${settlement.Y})`);
            const color = kingdomColors.get(settlement.KingdomId) || '#888888';

            let shape;
            switch (settlement.Type) {
                case 'Town':
                    shape = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    shape.setAttribute('cx', 0); shape.setAttribute('cy', 0); shape.setAttribute('r', 1.8);
                    break;
                case 'Castle':
                    shape = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    shape.setAttribute('x', -1.3); shape.setAttribute('y', -1.3); shape.setAttribute('width', 2.6); shape.setAttribute('height', 2.6);
                    break;
                case 'Village':
                    shape = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                    shape.setAttribute('points', '0,-1 1,1 -1,1');
                    break;
            }
            shape.setAttribute('fill', color);
            shape.setAttribute('stroke', '#fff');
            shape.setAttribute('stroke-width', 0.3);
            group.appendChild(shape);

            group.addEventListener('mouseenter', e => showTooltip(e, settlement));
            group.addEventListener('mousemove', e => updateTooltipPosition(e));
            group.addEventListener('mouseleave', hideTooltip);

            settlementMarkersGroup.appendChild(group);
        });
    }

    function updateLegend(kingdoms) {
        legendItems.innerHTML = '';
        kingdoms.forEach(k => {
            const item = document.createElement('div'); item.className = 'legend-item';
            const colorBox = document.createElement('div'); colorBox.className = 'legend-color'; colorBox.style.backgroundColor = k.Color;
            const name = document.createElement('span'); name.textContent = k.Name;
            item.appendChild(colorBox); item.appendChild(name); legendItems.appendChild(item);
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
        tooltip.style.left = e.clientX + 'px';
        tooltip.style.top = e.clientY + 'px';
    }
    function hideTooltip() { tooltip.style.display = 'none'; }
});
