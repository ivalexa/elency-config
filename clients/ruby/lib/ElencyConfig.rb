require 'ElencyConfig/Client'

class ElencyConfig
  def self.createClient
    client = Client.new()
    return client
  end
end