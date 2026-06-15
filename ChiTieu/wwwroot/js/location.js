window.chiTieuLocation = {
    getCurrentPosition: function () {
        return new Promise(function (resolve) {
            if (!window.isSecureContext) {
                resolve({
                    ok: false,
                    error: "Can mo app bang HTTPS de lay vi tri. Trinh duyet chan GPS tren HTTP, tru localhost."
                });
                return;
            }

            if (!navigator.geolocation) {
                resolve({
                    ok: false,
                    error: "Trinh duyet khong ho tro lay vi tri."
                });
                return;
            }

            navigator.geolocation.getCurrentPosition(
                function (position) {
                    resolve({
                        ok: true,
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy
                    });
                },
                function (error) {
                    var message = "Khong lay duoc vi tri.";
                    if (error.code === error.PERMISSION_DENIED) {
                        message = "Ban dang chan quyen vi tri. Hay vao cai dat trinh duyet/app va cho phep Location.";
                    } else if (error.code === error.POSITION_UNAVAILABLE) {
                        message = "Thiet bi chua co vi tri kha dung. Hay bat GPS/Wi-Fi/du lieu mang roi thu lai.";
                    } else if (error.code === error.TIMEOUT) {
                        message = "Lay vi tri qua lau. Hay ra noi thoang hon hoac bat GPS roi thu lai.";
                    }

                    resolve({
                        ok: false,
                        error: message
                    });
                },
                {
                    enableHighAccuracy: true,
                    timeout: 12000,
                    maximumAge: 60000
                });
        });
    }
};
