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
	const categoryDialog = document.getElementById("categoryDialog");
	const categoryForm = document.getElementById("categoryForm");
	const categoryParentFilter = document.getElementById("categoryParentFilter");
	const categoryParentId = document.getElementById("categoryParentId");
	const categoryImageInput = categoryForm?.querySelector('[name="Image"]');
	const addSubcategoryButton = document.getElementById("addSubcategoryButton");

	function applyCategoryFilter() {
		if (!categoryParentFilter) return;
		const selectedParentId = categoryParentFilter.value;
		const rows = [...document.querySelectorAll("#categoryTableBody [data-category-row]")];
		rows.forEach(row => {
			row.hidden = selectedParentId
				? row.dataset.parentId !== selectedParentId
				: false;
		});
		if (addSubcategoryButton) addSubcategoryButton.disabled = !selectedParentId;
		const visibleCount = rows.filter(row => !row.hidden).length;
		const summary = document.getElementById("categorySummary");
		if (summary) {
			const selectedOption = categoryParentFilter.selectedOptions[0];
			summary.textContent = selectedParentId
				? `${visibleCount} subcategories under ${selectedOption.textContent}.`
				: `${visibleCount} categories configured.`;
		}
	}

	function updateCategoryImageRequirement() {
		if (categoryImageInput) {
			categoryImageInput.required = !categoryForm.action.includes("/UpdateCategory/") && !categoryParentId.value;
		}
	}

	categoryParentFilter?.addEventListener("change", applyCategoryFilter);
	categoryParentId?.addEventListener("change", updateCategoryImageRequirement);
	document.querySelectorAll("[data-open-order]").forEach(button => button.addEventListener("click", () => orderDialog?.showModal()));
	document.getElementById("addCategoryButton")?.addEventListener("click", () => {
		if (!categoryForm) return;
		categoryForm.reset();
		categoryForm.action = categoryForm.dataset.createAction;
		categoryParentId.disabled = false;
		updateCategoryImageRequirement();
		document.getElementById("categoryCurrentImage").hidden = true;
		document.getElementById("categoryRemoveImage").value = "false";
		document.getElementById("categoryDialogTitle").textContent = "Add a category";
		document.getElementById("categorySubmitButton").textContent = "Create category";
		document.getElementById("categoryImageNote").hidden = true;
		categoryDialog?.showModal();
	});
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

	const dishForm = document.getElementById("menuForm");
	const dishTableBody = document.getElementById("dishTableBody");
	const dishSubmitButton = document.getElementById("dishSubmitButton");
	const dishImageInput = dishForm?.querySelector('[name="Image"]');

	function resetDishImageState() {
		document.getElementById("dishCurrentImage").hidden = true;
		document.getElementById("dishRemoveImage").value = "false";
	}

	function renderDishRow(dish) {
		const categoryName = dish.parentCategoryName
			? `${dish.parentCategoryName} · ${dish.categoryName}`
			: dish.categoryName;
		const actions = `${dishTableBody.dataset.canUpdate === "true"
			? `<button class="item-delete-button" type="button" data-dish-edit data-id="${dish.id}" data-name="${escapeHtml(dish.name)}" data-price="${dish.price}" data-old-price="${dish.oldPrice ?? ""}" data-description="${escapeHtml(dish.description)}" data-category-id="${dish.categoryId}" data-image-url="${escapeHtml(dish.imageUrl || "")}" data-is-new="${dish.isNew}" data-is-active="${dish.isActive}">Edit</button>`
			: ""}${dishTableBody.dataset.canDelete === "true"
			? `<button class="item-delete-button" type="button" data-dish-delete data-id="${dish.id}" data-name="${escapeHtml(dish.name)}">Delete</button>`
			: ""}`;
		const status = dishTableBody.dataset.canUpdate === "true"
			? `<label class="availability-toggle"><input type="checkbox" data-dish-status data-id="${dish.id}" data-name="${escapeHtml(dish.name)}" aria-label="${dish.isActive ? "Deactivate" : "Activate"} ${escapeHtml(dish.name)}" ${dish.isActive ? "checked" : ""} /><span aria-hidden="true"></span></label>`
			: `<label class="availability-toggle"><input type="checkbox" disabled aria-label="${dish.isActive ? "Active" : "Inactive"}: ${escapeHtml(dish.name)}" ${dish.isActive ? "checked" : ""} /><span aria-hidden="true"></span></label>`;
		return `<tr data-dish-row="${dish.id}">
			${actions ? `<td class="category-actions">${actions}</td>` : ""}
			<td>${dish.imageUrl
				? `<img class="dish-image" src="${escapeHtml(dish.imageUrl)}" alt="${escapeHtml(dish.name)}" loading="lazy" />`
				: '<span class="category-image-placeholder" aria-hidden="true">◈</span>'}</td>
			<td>${escapeHtml(dish.name)}</td>
			<td>${escapeHtml(categoryName)}</td>
			<td>${Number(dish.price).toFixed(2)}</td>
			<td>${dish.oldPrice == null ? "—" : Number(dish.oldPrice).toFixed(2)}</td>
			<td>${escapeHtml(dish.description || "")}</td>
			<td>${dish.isNew ? "Yes" : "—"}</td>
			<td>${status}</td>
		</tr>`;
	}

	function updateDishSummary() {
		const count = dishTableBody.querySelectorAll("[data-dish-row]").length;
		document.getElementById("dishSummary").textContent = `${count} dishes configured.`;
	}

	document.getElementById("addMenuButton")?.addEventListener("click", () => {
		dishForm.reset();
		dishForm.action = dishForm.dataset.createAction;
		dishImageInput.required = true;
		resetDishImageState();
		document.getElementById("dishSubmitButton").textContent = "Add dish";
		document.querySelector("#menuDialog .dialog-heading h2").textContent = "Add a dish";
		menuDialog?.showModal();
	});
	dishForm?.addEventListener("submit", async event => {
		event.preventDefault();
		dishSubmitButton.disabled = true;
		try {
			const image = dishImageInput.files[0];
			if (image) {
				const extension = image.name.split(".").pop().toLowerCase();
				const expectedType = extension === "png"
					? "image/png"
					: (extension === "jpg" || extension === "jpeg") ? "image/jpeg" : null;
				if (!expectedType || image.type && image.type !== expectedType) {
					notify("Choose a JPG, JPEG, or PNG image.");
					return;
				}
				if (image.size > 5 * 1024 * 1024) {
					notify("Choose a JPG, JPEG, or PNG image up to 5 MB.");
					return;
				}
			}
			const data = new FormData(dishForm);
			data.set("IsNew", dishForm.querySelector('[name="IsNew"]').checked ? "true" : "false");
			data.set("IsActive", dishForm.querySelector('[name="IsActive"]').checked ? "true" : "false");
			data.set("RemoveImage", dishForm.querySelector('[name="RemoveImage"]').value);
			const isUpdate = dishForm.action.includes("/UpdateDish/");
			const response = await fetch(dishForm.action, { method: "POST", body: data });
			const responseText = await response.text();
			let result;
			try {
				result = JSON.parse(responseText);
			} catch {
				throw new Error(response.ok
					? "The server returned an invalid response while saving the dish."
					: `Dish could not be saved (HTTP ${response.status}).`);
			}
			if (!response.ok) throw new Error(result.error || "Dish could not be saved.");

			dishTableBody.querySelector("td[colspan]")?.closest("tr")?.remove();
			const existingRow = dishTableBody.querySelector(`[data-dish-row="${result.id}"]`);
			if (existingRow) existingRow.outerHTML = renderDishRow(result);
			else dishTableBody.insertAdjacentHTML("beforeend", renderDishRow(result));
			updateDishSummary();
			dishForm.reset();
			dishImageInput.required = true;
			resetDishImageState();
			menuDialog.close();
			notify(result.warning || `${result.name} dish ${isUpdate ? "updated" : "added"}.`);
			dishForm.action = dishForm.dataset.createAction;
		} catch (error) {
			notify(error instanceof TypeError
				? "Could not reach the server while saving. Check your connection and try again."
				: error.message || "Dish could not be saved.");
		} finally {
			dishSubmitButton.disabled = false;
		}
	});
	document.addEventListener("change", async event => {
		const toggle = event.target.closest("[data-dish-status], [data-category-status]");
		if (!toggle) return;

		const isDishStatus = toggle.hasAttribute("data-dish-status");
		const tableBody = toggle.closest("#dishTableBody, #categoryTableBody");
		const form = document.getElementById(isDishStatus ? "menuForm" : "categoryForm");
		const isActive = toggle.checked;
		const previousStatus = !isActive;
		toggle.disabled = true;
		toggle.setAttribute("aria-busy", "true");

		try {
			const data = new FormData();
			data.append("__RequestVerificationToken", form.querySelector('[name="__RequestVerificationToken"]').value);
			data.append("isActive", String(isActive));
			const response = await fetch(`${tableBody.dataset.statusUrl}/${encodeURIComponent(toggle.dataset.id)}`, {
				method: "POST",
				body: data
			});
			const responseText = await response.text();
			let result;
			try {
				result = JSON.parse(responseText);
			} catch {
				const itemType = isDishStatus ? "Dish" : "Category";
				throw new Error(response.ok
					? `The server returned an invalid response while updating ${itemType.toLowerCase()} status.`
					: `${itemType} status could not be updated (HTTP ${response.status}).`);
			}
			const itemType = isDishStatus ? "Dish" : "Category";
			if (!response.ok) throw new Error(result.error || `${itemType} status could not be updated.`);

			toggle.checked = result.isActive;
			toggle.setAttribute("aria-label", `${result.isActive ? "Deactivate" : "Activate"} ${toggle.dataset.name}`);
			if (isDishStatus) {
				const editButton = toggle.closest("[data-dish-row]").querySelector("[data-dish-edit]");
				if (editButton) editButton.dataset.isActive = String(result.isActive);
			}
			if (result.warning) notify(result.warning);
		} catch (error) {
			toggle.checked = previousStatus;
			notify(error instanceof TypeError
				? "Could not reach the server while updating status. Check your connection and try again."
				: error.message || `${isDishStatus ? "Dish" : "Category"} status could not be updated.`);
		} finally {
			toggle.disabled = false;
			toggle.removeAttribute("aria-busy");
		}
	});
	document.addEventListener("click", async event => {
		const editButton = event.target.closest("[data-dish-edit]");
		if (editButton && dishForm) {
			dishForm.reset();
			dishForm.action = `${dishTableBody.dataset.updateUrl}/${encodeURIComponent(editButton.dataset.id)}`;
			dishForm.querySelector('[name="Name"]').value = editButton.dataset.name;
			dishForm.querySelector('[name="CategoryId"]').value = editButton.dataset.categoryId;
			dishForm.querySelector('[name="Price"]').value = editButton.dataset.price;
			dishForm.querySelector('[name="OldPrice"]').value = editButton.dataset.oldPrice;
			dishForm.querySelector('[name="Description"]').value = editButton.dataset.description;
			dishForm.querySelector('[name="IsNew"]').checked = editButton.dataset.isNew === "true";
			dishForm.querySelector('[name="IsActive"]').checked = editButton.dataset.isActive === "true";
			dishImageInput.required = false;
			document.getElementById("dishRemoveImage").value = "false";
			const currentImage = document.getElementById("dishCurrentImage");
			if (editButton.dataset.imageUrl) {
				const preview = document.getElementById("dishImagePreview");
				const download = document.getElementById("dishImageDownload");
				const downloadUrl = new URL(editButton.dataset.imageUrl, window.location.origin);
				preview.src = editButton.dataset.imageUrl;
				preview.alt = `${editButton.dataset.name} dish image`;
				if (downloadUrl.hostname === "res.cloudinary.com") {
					downloadUrl.pathname = downloadUrl.pathname.replace("/image/upload/", "/image/upload/fl_attachment/");
				}
				download.href = downloadUrl.toString();
				download.download = `${editButton.dataset.name}-dish`;
				currentImage.hidden = false;
			} else {
				currentImage.hidden = true;
			}
			document.querySelector("#menuDialog .dialog-heading h2").textContent = "Edit dish";
			dishSubmitButton.textContent = "Save changes";
			menuDialog?.showModal();
			return;
		}

		const removeDishImageButton = event.target.closest("#dishImageRemove");
		if (removeDishImageButton && dishForm) {
			document.getElementById("dishRemoveImage").value = "true";
			document.getElementById("dishCurrentImage").hidden = true;
			return;
		}

		const deleteButton = event.target.closest("[data-dish-delete]");
		if (!deleteButton || !dishTableBody || !window.confirm(`Delete "${deleteButton.dataset.name}"?`)) return;

		deleteButton.disabled = true;
		try {
			const data = new FormData();
			data.append("__RequestVerificationToken", dishForm.querySelector('[name="__RequestVerificationToken"]').value);
			const response = await fetch(`${dishTableBody.dataset.deleteUrl}/${encodeURIComponent(deleteButton.dataset.id)}`, {
				method: "POST",
				body: data
			});
			const responseText = await response.text();
			let result;
			try {
				result = JSON.parse(responseText);
			} catch {
				throw new Error(response.ok
					? "The server returned an invalid response while deleting the dish."
					: `Dish could not be deleted (HTTP ${response.status}).`);
			}
			if (!response.ok) throw new Error(result.error || "Dish could not be deleted.");
			deleteButton.closest("[data-dish-row]").remove();
			if (dishTableBody.querySelectorAll("[data-dish-row]").length === 0) {
				const columnCount = dishTableBody.dataset.canUpdate === "true" || dishTableBody.dataset.canDelete === "true" ? 9 : 8;
				dishTableBody.innerHTML = `<tr><td colspan="${columnCount}">No dishes have been added yet.</td></tr>`;
			}
			updateDishSummary();
			notify(result.warning || `${deleteButton.dataset.name} dish deleted.`);
		} catch (error) {
			notify(error instanceof TypeError
				? "Could not reach the server while deleting. Check your connection and try again."
				: error.message || "Dish could not be deleted.");
			deleteButton.disabled = false;
		}
	});
	categoryForm?.addEventListener("submit", async event => {
		event.preventDefault();
		const form = event.currentTarget;
		const submitButton = form.querySelector('[type="submit"]');
		const imageInput = form.querySelector('[name="Image"]');
		const isUpdate = form.action.includes("/UpdateCategory/");
		const image = imageInput.files[0];
		if (image) {
			const extension = image.name.split(".").pop().toLowerCase();
			const expectedType = extension === "png"
				? "image/png"
				: (extension === "jpg" || extension === "jpeg") ? "image/jpeg" : null;
			const supportedType = expectedType && (!image.type || image.type === expectedType);
			if (!supportedType) {
				notify("Choose a JPG, JPEG, or PNG image.");
				return;
			}
		}
		if (image && image.size > 5 * 1024 * 1024) {
			notify("Choose a JPG, JPEG, or PNG image up to 5 MB.");
			return;
		}
		submitButton.disabled = true;

		try {
			const data = new FormData(form);
			if (isUpdate && !imageInput.files.length) data.delete("Image");
			data.set("RemoveImage", form.querySelector('[name="RemoveImage"]').value);
			const response = await fetch(form.action, { method: "POST", body: data });
			const responseText = await response.text();
			let result;
			try {
				result = JSON.parse(responseText);
			} catch {
				throw new Error(response.ok
					? "The server returned an invalid response while saving the category."
					: `Category could not be saved (HTTP ${response.status}).`);
			}
			if (!response.ok) throw new Error(result.error || "Category could not be saved.");
			const tableBody = document.getElementById("categoryTableBody");
			tableBody.querySelector("td[colspan]")?.closest("tr")?.remove();
			const actions = `${tableBody.dataset.canUpdate === "true"
				? `<button class="item-delete-button" type="button" data-category-edit data-id="${result.id}" data-name="${escapeHtml(result.name)}" data-details="${escapeHtml(result.details)}" data-parent-id="${result.parentId ?? ""}">Edit</button>`
				: ""}${tableBody.dataset.canDelete === "true"
				? `<button class="item-delete-button" type="button" data-category-delete data-id="${result.id}" data-name="${escapeHtml(result.name)}">Delete</button>`
				: ""}`;
			const status = tableBody.dataset.canUpdate === "true"
				? `<label class="availability-toggle"><input type="checkbox" data-category-status data-id="${result.id}" data-name="${escapeHtml(result.name)}" aria-label="${result.isActive ? "Deactivate" : "Activate"} ${escapeHtml(result.name)}" ${result.isActive ? "checked" : ""} /><span aria-hidden="true"></span></label>`
				: `<label class="availability-toggle"><input type="checkbox" disabled aria-label="${result.isActive ? "Active" : "Inactive"}: ${escapeHtml(result.name)}" ${result.isActive ? "checked" : ""} /><span aria-hidden="true"></span></label>`;
			const rowHtml = `<tr data-category-row="${result.id}" data-parent-id="${result.parentId ?? ""}">
				${actions ? `<td class="category-actions">${actions}</td>` : ""}
				<td>${result.imageUrl
					? `<img class="category-image" src="${escapeHtml(result.imageUrl)}" alt="${escapeHtml(result.name)}" loading="lazy" />`
					: '<span class="category-image-placeholder" aria-hidden="true">◈</span>'}</td>
				<td>${escapeHtml(result.name)}${result.parentId
					? `<small class="category-parent-label">Subcategory of ${escapeHtml(categoryParentId.selectedOptions[0]?.textContent || "")}</small>`
					: ""}</td>
				<td>${escapeHtml(result.details)}</td>
				<td>${status}</td>
			</tr>`;
			const existingRow = tableBody.querySelector(`[data-category-row="${result.id}"]`);
			if (existingRow) {
				existingRow.outerHTML = rowHtml;
			} else {
				tableBody.insertAdjacentHTML("beforeend", rowHtml);
			}
			if (!result.parentId && categoryParentFilter
				&& ![...categoryParentFilter.options].some(option => option.value === String(result.id))) {
				categoryParentFilter.add(new Option(result.name, result.id));
				categoryParentId.add(new Option(result.name, result.id));
			}
			applyCategoryFilter();
			form.reset();
			categoryParentId.disabled = false;
			updateCategoryImageRequirement();
			document.getElementById("categoryCurrentImage").hidden = true;
			categoryDialog.close();
			notify(result.warning || `${result.name} category ${isUpdate ? "updated" : "created"}.`);
		} catch (error) {
			notify(error instanceof TypeError
				? "Could not reach the server while saving. Check your connection and try again."
				: error.message || "Category could not be saved.");
		} finally {
			submitButton.disabled = false;
		}
	});
	addSubcategoryButton?.addEventListener("click", () => {
		if (!categoryForm || !categoryParentFilter.value) return;
		categoryForm.reset();
		categoryForm.action = categoryForm.dataset.createAction;
		categoryParentId.disabled = false;
		categoryParentId.value = categoryParentFilter.value;
		updateCategoryImageRequirement();
		document.getElementById("categoryCurrentImage").hidden = true;
		document.getElementById("categoryRemoveImage").value = "false";
		document.getElementById("categoryDialogTitle").textContent = "Add a subcategory";
		document.getElementById("categorySubmitButton").textContent = "Add subcategory";
		document.getElementById("categoryImageNote").hidden = false;
		categoryDialog?.showModal();
	});
	document.addEventListener("click", async event => {
		const editButton = event.target.closest("[data-category-edit]");
		if (editButton && categoryForm) {
			categoryForm.reset();
			categoryForm.action = `${document.getElementById("categoryTableBody").dataset.updateUrl}/${encodeURIComponent(editButton.dataset.id)}`;
			categoryForm.querySelector('[name="Name"]').value = editButton.dataset.name;
			categoryForm.querySelector('[name="Details"]').value = editButton.dataset.details;
			categoryParentId.value = editButton.dataset.parentId || "";
			categoryParentId.disabled = true;
			updateCategoryImageRequirement();
			const existingImage = editButton.closest("tr").querySelector(".category-image");
			const currentImage = document.getElementById("categoryCurrentImage");
			if (existingImage) {
				const preview = document.getElementById("categoryImagePreview");
				const download = document.getElementById("categoryImageDownload");
				const downloadUrl = new URL(existingImage.src);
				preview.src = existingImage.src;
				preview.alt = `${editButton.dataset.name} category image`;
				if (downloadUrl.hostname === "res.cloudinary.com") {
					downloadUrl.pathname = downloadUrl.pathname.replace("/image/upload/", "/image/upload/fl_attachment/");
				}
				download.href = downloadUrl.toString();
				download.download = `${editButton.dataset.name}-category`;
				currentImage.hidden = false;
			} else {
				currentImage.hidden = true;
			}
			document.getElementById("categoryRemoveImage").value = "false";
			document.getElementById("categoryDialogTitle").textContent = "Edit category";
			document.getElementById("categorySubmitButton").textContent = "Save changes";
			document.getElementById("categoryImageNote").hidden = false;
			categoryDialog?.showModal();
			return;
		}

		const removeImageButton = event.target.closest("#categoryImageRemove");
		if (removeImageButton && categoryForm) {
			document.getElementById("categoryRemoveImage").value = "true";
			document.getElementById("categoryCurrentImage").hidden = true;
			return;
		}

		const deleteButton = event.target.closest("[data-category-delete]");
		if (!deleteButton || !categoryForm || !window.confirm(`Delete "${deleteButton.dataset.name}" and its image?`)) return;
		deleteButton.disabled = true;
		try {
			const tableBody = document.getElementById("categoryTableBody");
			const data = new FormData();
			data.append("__RequestVerificationToken", categoryForm.querySelector('[name="__RequestVerificationToken"]').value);
			const response = await fetch(`${tableBody.dataset.deleteUrl}/${encodeURIComponent(deleteButton.dataset.id)}`, {
				method: "POST",
				body: data
			});
			const result = await response.json();
			if (!response.ok) throw new Error(result.error || "Category could not be deleted.");
			deleteButton.closest("tr").remove();
			const remaining = tableBody.querySelectorAll("[data-category-row]").length;
			const summary = document.querySelector('[data-page="categories"] .page-panel .panel-heading p');
			if (summary) summary.textContent = `${remaining} categories configured.`;
			if (remaining === 0) {
				const columnCount = tableBody.dataset.canUpdate === "true" || tableBody.dataset.canDelete === "true" ? 5 : 4;
				tableBody.innerHTML = `<tr><td colspan="${columnCount}">No categories have been added yet.</td></tr>`;
			}
			notify(result.warning || `${deleteButton.dataset.name} category deleted.`);
		} catch (error) {
			notify(error.message || "Category could not be deleted.");
			deleteButton.disabled = false;
		}
	});
})();
