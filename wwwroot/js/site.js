(() => {
	const passwordToggle = document.querySelector("[data-password-toggle]");
	if (passwordToggle) {
		passwordToggle.addEventListener("click", () => {
			const password = document.getElementById("password");
			const reveal = password.type === "password";
			password.type = reveal ? "text" : "password";
			passwordToggle.textContent = reveal ? "Hide" : "Show";
			passwordToggle.setAttribute("aria-label", reveal ? "Hide password" : "Show password");
		});
	}

	const shell = document.getElementById("appShell");
	if (!shell) return;

	const sidebarToggle = document.getElementById("sidebarToggle");
	const mobileScrim = document.getElementById("mobileScrim");
	const mainArea = document.getElementById("mainArea");
	const searchInput = document.getElementById("globalSearch");
	const searchLabel = searchInput.closest(".global-search");
	const chartBars = document.getElementById("chartBars");
	const revenueTotal = document.getElementById("revenueTotal");
	const toast = document.getElementById("toastMessage");
	const chartData = {
		week: { total: "$2,408.50", labels: ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], values: [42, 67, 53, 81, 59, 95, 72] },
		month: { total: "$8,561.00", labels: ["Wk 1", "Wk 2", "Wk 3", "Wk 4"], values: [55, 72, 61, 91] },
		today: { total: "$684.25", labels: ["9 AM", "11 AM", "1 PM", "3 PM", "5 PM", "7 PM"], values: [28, 44, 77, 59, 92, 69] }
	};
	const orders = [
		{ id: "#AK-2048", customer: "Maya Patel", items: "2 items", time: "12:42 PM", total: 24.5, status: "Preparing" },
		{ id: "#AK-2047", customer: "Rohan Mehta", items: "3 items", time: "12:36 PM", total: 38, status: "Ready" },
		{ id: "#AK-2046", customer: "Sana Khan", items: "1 item", time: "12:21 PM", total: 16.75, status: "Served" },
		{ id: "#AK-2045", customer: "Arjun Shah", items: "4 items", time: "12:08 PM", total: 52, status: "Preparing" },
		{ id: "#AK-2044", customer: "Neha Joshi", items: "2 items", time: "11:54 AM", total: 29.5, status: "Ready" }
	];
	let toastTimeout;

	function notify(message) {
		toast.textContent = message;
		toast.classList.add("is-visible");
		clearTimeout(toastTimeout);
		toastTimeout = setTimeout(() => toast.classList.remove("is-visible"), 2600);
	}

	function escapeHtml(value) {
		return String(value).replace(/[&<>"']/g, character => ({
			"&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;"
		})[character]);
	}

	function renderChart(period = "week") {
		if (!chartBars || !revenueTotal) return;
		const data = chartData[period];
		chartBars.innerHTML = data.values.map((value, index) => `
			<div class="chart-column ${index === data.values.length - 1 ? "is-highlight" : ""}">
				<div class="chart-bar" style="height:${value}%" title="${data.labels[index]}: $${(value * 32.5).toFixed(0)}"></div>
				<span>${data.labels[index]}</span>
			</div>`).join("");
		revenueTotal.textContent = data.total;
	}

	function applySearch() {
		const activePage = document.querySelector(".page-view:not(.is-hidden)");
		if (!activePage) return;
		const query = searchInput.value.trim().toLowerCase();
		activePage.querySelectorAll("[data-search-item]").forEach(item => {
			item.hidden = query.length > 0 && !item.dataset.searchItem.toLowerCase().includes(query);
		});
	}

	function renderOrders() {
		document.querySelectorAll("[data-orders-body]").forEach(body => {
			const compact = body.dataset.compact === "true";
			const canUpdate = body.dataset.canUpdate === "true";
			const canDelete = body.dataset.canDelete === "true";
			const filter = document.getElementById("orderFilter")?.value ?? "all";
			const visible = orders.filter(order => filter === "all" || order.status === filter);
			body.innerHTML = (compact ? visible.slice(0, 3) : visible).map(order => `
				<tr class="searchable" data-search-item="${escapeHtml(`${order.id} ${order.customer} ${order.items} ${order.status}`.toLowerCase())}">
					<td class="order-id">${escapeHtml(order.id)}</td><td>${escapeHtml(order.customer)}</td>
					${compact ? "" : `<td>${escapeHtml(order.items)}</td><td>${escapeHtml(order.time)}</td>`}
					<td>$${order.total.toFixed(2)}</td>
					<td>${canUpdate
						? `<button class="status-pill status-${order.status.toLowerCase()}" type="button" data-order-status="${escapeHtml(order.id)}" title="Click to update status">${escapeHtml(order.status)}</button>`
						: `<span class="status-pill status-${order.status.toLowerCase()}">${escapeHtml(order.status)}</span>`}</td>
					${canDelete ? `<td><button class="item-delete-button" type="button" data-order-delete="${escapeHtml(order.id)}">Delete</button></td>` : ""}
				</tr>`).join("");
		});
		applySearch();
	}

	function showPage(pageName) {
		document.querySelectorAll(".page-view").forEach(page => page.classList.toggle("is-hidden", page.dataset.page !== pageName));
		searchInput.value = "";
		searchLabel.classList.remove("is-open");
		applySearch();
		if (window.matchMedia("(max-width: 700px)").matches) shell.classList.remove("is-mobile-open");
	}

	function closePopovers() {
		document.querySelectorAll("[data-popover-toggle]").forEach(button => {
			button.setAttribute("aria-expanded", "false");
			document.getElementById(button.dataset.popoverToggle).hidden = true;
		});
	}

	function updateSidebarButton() {
		const mobile = window.matchMedia("(max-width: 700px)").matches;
		sidebarToggle.setAttribute("aria-expanded", mobile
			? String(shell.classList.contains("is-mobile-open"))
			: String(!shell.classList.contains("is-collapsed")));
	}

	renderChart();
	renderOrders();
	showPage(mainArea.dataset.initialPage || "dashboard");
	try {
		if (localStorage.getItem("anjani-sidebar-collapsed") === "true" && !window.matchMedia("(max-width: 700px)").matches) shell.classList.add("is-collapsed");
	} catch { }
	updateSidebarButton();

	sidebarToggle.addEventListener("click", () => {
		if (window.matchMedia("(max-width: 700px)").matches) shell.classList.toggle("is-mobile-open");
		else {
			shell.classList.toggle("is-collapsed");
			try { localStorage.setItem("anjani-sidebar-collapsed", String(shell.classList.contains("is-collapsed"))); } catch { }
		}
		updateSidebarButton();
	});
	mobileScrim.addEventListener("click", () => { shell.classList.remove("is-mobile-open"); updateSidebarButton(); });

	document.querySelectorAll("[data-menu-group-toggle]").forEach(button => button.addEventListener("click", () => {
		const submenu = document.getElementById(button.dataset.menuGroupToggle);
		const collapsed = submenu.classList.toggle("is-collapsed");
		button.classList.toggle("submenu-closed", collapsed);
		button.setAttribute("aria-expanded", String(!collapsed));
	}));

	searchInput.addEventListener("input", applySearch);
	searchInput.addEventListener("focus", () => searchLabel.classList.add("is-open"));
	searchLabel.addEventListener("click", event => {
		if (!window.matchMedia("(max-width: 700px)").matches || event.target === searchInput || searchLabel.classList.contains("is-open")) return;
		event.preventDefault();
		searchLabel.classList.add("is-open");
		searchInput.focus();
	});
	document.addEventListener("click", event => {
		if (!searchLabel.contains(event.target) && !searchInput.value) searchLabel.classList.remove("is-open");
		if (!event.target.closest(".popover-wrap")) closePopovers();
	});
	document.addEventListener("keydown", event => {
		if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
			event.preventDefault(); searchLabel.classList.add("is-open"); searchInput.focus();
		}
		if (event.key === "Escape") { closePopovers(); shell.classList.remove("is-mobile-open"); updateSidebarButton(); }
	});

	document.querySelectorAll("[data-popover-toggle]").forEach(button => button.addEventListener("click", event => {
		event.stopPropagation();
		const panel = document.getElementById(button.dataset.popoverToggle);
		const shouldOpen = panel.hidden;
		closePopovers(); panel.hidden = !shouldOpen;
		button.setAttribute("aria-expanded", String(shouldOpen));
	}));
	document.querySelectorAll("[data-period]").forEach(button => button.addEventListener("click", () => {
		document.querySelectorAll("[data-period]").forEach(item => item.classList.toggle("is-selected", item === button));
		renderChart(button.dataset.period);
	}));

	document.getElementById("orderFilter")?.addEventListener("change", renderOrders);
	document.addEventListener("click", event => {
		const deleteButton = event.target.closest("[data-order-delete]");
		if (deleteButton) {
			const body = deleteButton.closest("[data-orders-body]");
			if (body?.dataset.canDelete !== "true") return;
			const index = orders.findIndex(item => item.id === deleteButton.dataset.orderDelete);
			if (index !== -1) {
				const [removed] = orders.splice(index, 1);
				const orderMetric = document.querySelector('[data-metric="orders"]');
				if (orderMetric) orderMetric.textContent = String(247 + orders.length - 5);
				renderOrders();
				notify(`${removed.id} was removed.`);
			}
			return;
		}

		const statusButton = event.target.closest("[data-order-status]");
		if (!statusButton) return;
		if (statusButton.closest("[data-orders-body]")?.dataset.canUpdate !== "true") return;
		const order = orders.find(item => item.id === statusButton.dataset.orderStatus);
		if (!order) return;
		const statuses = ["Preparing", "Ready", "Served"];
		order.status = statuses[(statuses.indexOf(order.status) + 1) % statuses.length];
		renderOrders(); notify(`${order.id} is now ${order.status.toLowerCase()}.`);
	});

	const orderDialog = document.getElementById("orderDialog");
	const menuDialog = document.getElementById("menuDialog");
	document.querySelectorAll("[data-open-order]").forEach(button => button.addEventListener("click", () => orderDialog?.showModal()));
	document.querySelectorAll("[data-close-dialog]").forEach(button => button.addEventListener("click", () => button.closest("dialog").close()));
	document.getElementById("orderForm")?.addEventListener("submit", event => {
		event.preventDefault();
		const form = event.currentTarget;
		const values = new FormData(form);
		const number = 2044 + orders.length;
		orders.unshift({ id: `#AK-${number}`, customer: values.get("customer").trim(), items: values.get("items").trim(), time: "Just now", total: Number(values.get("total")), status: "Preparing" });
		const orderMetric = document.querySelector('[data-metric="orders"]');
		if (orderMetric) orderMetric.textContent = String(247 + orders.length - 5);
		renderOrders(); form.reset(); orderDialog.close(); notify("Order added to the kitchen queue.");
	});

	document.getElementById("addMenuButton")?.addEventListener("click", () => menuDialog?.showModal());
	document.getElementById("menuForm")?.addEventListener("submit", event => {
		event.preventDefault();
		const form = event.currentTarget;
		const values = new FormData(form);
		const name = values.get("name").trim();
		const category = values.get("category").trim();
		const price = Number(values.get("price")).toFixed(2);
		const item = document.createElement("article");
		item.className = "managed-menu searchable";
		item.dataset.searchItem = `${name} ${category} ${price}`.toLowerCase();
		const menuList = document.querySelector(".menu-management-list");
		const canUpdate = menuList.dataset.canUpdate === "true";
		const canDelete = menuList.dataset.canDelete === "true";
		item.innerHTML = `<span class="dish-thumb dish-one">${escapeHtml(name.charAt(0).toUpperCase())}</span><div class="dish-info"><strong>${escapeHtml(name)}</strong><small>${escapeHtml(category)} · $${price}</small></div><span class="menu-sold">New item</span><label class="availability-toggle"><input type="checkbox" checked ${canUpdate ? "" : "disabled"} /><span></span><small>Available</small></label>${canDelete ? '<button class="item-delete-button" type="button" data-dish-delete>Delete</button>' : ""}`;
		menuList.prepend(item);
		document.querySelector(".menu-summary").textContent = `${document.querySelectorAll(".managed-menu").length} dishes`;
		const metric = document.querySelector('[data-metric="menus"]');
		if (metric) metric.textContent = String(Number(metric.textContent) + 1);
		form.reset(); menuDialog.close(); notify(`${name} added to your menu.`);
	});
	document.addEventListener("change", event => {
		if (!event.target.matches(".availability-toggle input")) return;
		if (event.target.disabled) return;
		const label = event.target.closest(".availability-toggle").querySelector("small");
		label.textContent = event.target.checked ? "Available" : "Sold out";
		notify(`${event.target.closest(".managed-menu").querySelector(".dish-info strong").textContent} marked ${label.textContent.toLowerCase()}.`);
	});
	document.addEventListener("click", event => {
		const deleteButton = event.target.closest("[data-dish-delete]");
		if (deleteButton) {
			const menuList = deleteButton.closest(".menu-management-list");
			if (menuList?.dataset.canDelete !== "true") return;
			deleteButton.closest(".managed-menu").remove();
			document.querySelector(".menu-summary").textContent = `${document.querySelectorAll(".managed-menu").length} dishes`;
			const metric = document.querySelector('[data-metric="menus"]');
			if (metric) metric.textContent = String(Math.max(0, Number(metric.textContent) - 1));
			notify("Dish removed from your menu.");
			return;
		}
	});
})();
