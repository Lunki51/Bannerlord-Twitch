< !--Campaign Map-- >
  $(document).ready(function () {
    const mapSvg = document.getElementById('campaign-map-svg');
    const terrainZonesGroup = document.getElementById('terrain-zones');
    const settlementMarkersGroup = document.getElementById('settlement-markers');
    const legendItems = document.getElementById('legend-items');
    const tooltip = document.getElementById('map-tooltip');
    const tooltipName = document.getElementById('tooltip-name');
    const tooltipInfo = document.getElementById('tooltip-info');

    let currentMapData = null;
    let kingdomColors = new Map();

    // Initialize SignalR connection
    if (typeof $.connection.mapHub !== 'undefined') {
      $.connection.hub.url = '$url_root$/signalr';

      const mapHub = $.connection.mapHub;

      // Handle map updates from game
        mapHub.client.updateMap = function (mapData) {
            const container = document.getElementById('campaign-map-container');

            if (!mapData) {
                // Hide the entire overlay if no data (e.g., during a Battle)
                container.style.display = 'none';
                return;
            }

            container.style.display = 'block';
            console.log('Received map data:', mapData);
            currentMapData = mapData;
            renderMap(mapData);
        };

      // Connection event handlers
      $.connection.hub.error(function (error) {
        console.log('Map Hub error: ' + error);
      });

      $.connection.hub.starting(function () {
        console.log('Map Hub starting');
      });

      $.connection.hub.reconnected(function () {
        console.log('Map Hub reconnected');
        mapHub.server.refresh();
      });

      $.connection.hub.disconnected(function () {
        console.log('Map Hub disconnected');
      });

      // Start connection
      $.connection.hub.start()
        .done(function () {
          console.log('Map Hub connected');
          mapHub.server.refresh();
        })
        .fail(function () {
          console.log('Map Hub connection failed');
          legendItems.innerHTML = '<div style="color: #f44; font-size: 11px;">Connection failed</div>';
        });
    } else {
      console.log('Map Hub not available');
      legendItems.innerHTML = '<div style="color: #888; font-size: 11px;">Map Hub not available</div>';
    }

    function renderMap(data) {
      if (!data) return;

      currentMapData = data;
      kingdomColors.clear();

      if (data.Kingdoms) {
        data.Kingdoms.forEach(k => {
          kingdomColors.set(k.Id, k.Color);
        });
      }

      renderTerrainZones(data.TerrainZones || []);
      renderSettlements(data.Settlements || []);
      updateLegend(data.Kingdoms || []);
    }

    function renderTerrainZones(zones) {
      terrainZonesGroup.innerHTML = '';

      zones.forEach(zone => {
        if (!zone.Points || zone.Points.length < 3) return;

        const polygon = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
        const points = zone.Points.map(p => `${p[0]},${p[1]}`).join(' ');

        polygon.setAttribute('points', points);
        polygon.setAttribute('class', `terrain-zone ${zone.Type}`);

        // Add pattern fill for sea and blocked zones
        if (zone.Type === 'sea') {
          polygon.setAttribute('fill', 'url(#seaPattern)');
        } else if (zone.Type === 'blocked') {
          polygon.setAttribute('fill', 'url(#mountainPattern)');
        }

        terrainZonesGroup.appendChild(polygon);

        // Add zone labels
        if (zone.Points.length > 0) {
          const centerX = zone.Points.reduce((sum, p) => sum + p[0], 0) / zone.Points.length;
          const centerY = zone.Points.reduce((sum, p) => sum + p[1], 0) / zone.Points.length;

          const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
          label.setAttribute('x', centerX);
          label.setAttribute('y', centerY);
          label.setAttribute('class', 'zone-label');
          label.textContent = zone.Type === 'sea' ? 'Sea' :
            zone.Type === 'blocked' ? 'Impassable' : '';

          if (label.textContent) {
            terrainZonesGroup.appendChild(label);
          }
        }
      });
    }

    function renderSettlements(settlements) {
      settlementMarkersGroup.innerHTML = '';

      settlements.forEach(settlement => {
        const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        group.setAttribute('class', `settlement-marker ${settlement.Type}`);
        group.setAttribute('transform', `translate(${settlement.X}, ${settlement.Y})`);

        const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        circle.setAttribute('cx', '0');
        circle.setAttribute('cy', '0');

        const color = kingdomColors.get(settlement.KingdomId) || '#888888';
        circle.setAttribute('fill', color);

        const icon = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        icon.setAttribute('x', '0');
        icon.setAttribute('y', '0.4');
        icon.setAttribute('class', 'settlement-icon');
        icon.textContent = settlement.Type === 'Town' ? '●' :
          settlement.Type === 'Castle' ? '■' : '▪';

        group.appendChild(circle);
        group.appendChild(icon);

        // Add hover events
        group.addEventListener('mouseenter', (e) => {
          showTooltip(e, settlement);
        });

        group.addEventListener('mousemove', (e) => {
          updateTooltipPosition(e);
        });

        group.addEventListener('mouseleave', () => {
          hideTooltip();
        });

        settlementMarkersGroup.appendChild(group);
      });
    }

    function updateLegend(kingdoms) {
      legendItems.innerHTML = '';

      if (!kingdoms || kingdoms.length === 0) {
        legendItems.innerHTML = '<div style="color: #888; font-size: 11px;">No kingdoms data</div>';
        return;
      }

      kingdoms.forEach(kingdom => {
        const item = document.createElement('div');
        item.className = 'legend-item';

        const colorBox = document.createElement('div');
        colorBox.className = 'legend-color';
        colorBox.style.backgroundColor = kingdom.Color;

        const name = document.createElement('span');
        name.textContent = kingdom.Name;

        item.appendChild(colorBox);
        item.appendChild(name);
        legendItems.appendChild(item);
      });
    }

    function showTooltip(e, settlement) {
      const kingdom = currentMapData?.Kingdoms?.find(k => k.Id === settlement.KingdomId);
      const kingdomName = kingdom ? kingdom.Name : 'Neutral';

      tooltipName.textContent = settlement.Name;
      tooltipInfo.innerHTML = `${settlement.Type}<br>Kingdom: ${kingdomName}`;

      tooltip.style.display = 'block';
      updateTooltipPosition(e);
    }

    function updateTooltipPosition(e) {
      tooltip.style.left = e.clientX + 'px';
      tooltip.style.top = e.clientY + 'px';
    }

    function hideTooltip() {
      tooltip.style.display = 'none';
    }
  });